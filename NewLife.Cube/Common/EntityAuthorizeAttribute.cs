﻿using System;
using System.Web;
using System.Web.Mvc;
using NewLife.Log;
using XCode.Membership;

namespace NewLife.Cube
{
    /// <summary>实体授权特性</summary>
    public class EntityAuthorizeAttribute : AuthorizeAttribute
    {
        #region 属性
        /// <summary>资源名称。需要增加新菜单而不需要控制器名称时，指定资源名称</summary>
        public String ResourceName { get; set; }

        /// <summary>授权项</summary>
        public PermissionFlags Permission { get; set; }

        /// <summary>是否全局特性</summary>
        internal Boolean IsGlobal;
        #endregion

        #region 构造
        /// <summary>实例化实体授权特性</summary>
        public EntityAuthorizeAttribute() { }

        /// <summary>实例化实体授权特性</summary>
        /// <param name="permission"></param>
        public EntityAuthorizeAttribute(PermissionFlags permission = PermissionFlags.None)
        {
            //ResourceName = resourceName;
            Permission = permission;
        }
        #endregion

        #region 方法
        /// <summary>授权核心</summary>
        /// <param name="httpContext"></param>
        /// <returns></returns>
        protected override Boolean AuthorizeCore(HttpContextBase httpContext)
        {
            var user = httpContext.User?.Identity as IUser;
            return user != null;
        }

        /// <summary>授权发生时触发</summary>
        /// <param name="filterContext"></param>
        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            // 只验证管辖范围
            if (!AreaRegistrationBase.Contains(filterContext.Controller)) return;

            ManageProvider.Provider.SetPrincipal();

            var act = filterContext.ActionDescriptor;

            // 如果控制器或者Action放有该特性，则跳过全局
            if (IsGlobal)
            {
                if (act.IsDefined(typeof(EntityAuthorizeAttribute), true) || act.ControllerDescriptor.IsDefined(typeof(EntityAuthorizeAttribute), true)) return;
            }

            // 允许匿名访问时，直接跳过检查
            if (act.IsDefined(typeof(AllowAnonymousAttribute), true) || act.ControllerDescriptor.IsDefined(typeof(AllowAnonymousAttribute), true)) return;

            // 判断当前登录用户
            var user = ManageProvider.User;
            if (user == null)
            {
                //HandleUnauthorizedRequest(filterContext);
                var ctx = filterContext.HttpContext;
                var rurl = HttpRuntime.AppDomainAppVirtualPath.EnsureEnd("/");
                rurl += "Admin/User/Login";

                var retUrl = ctx.Request.Url?.PathAndQuery;
                if (!retUrl.IsNullOrEmpty() && retUrl != "/") rurl += "?returnUrl=" + retUrl;

                filterContext.HttpContext.Response.Redirect(rurl, true);
                return;
            }

            // 根据请求Url定位资源菜单
            var url = filterContext.HttpContext.Request.AppRelativeCurrentExecutionFilePath;
            var menu = ManageProvider.Menu?.Current;
            if (menu != null)
            {
                var role = user?.Role;
                if (role != null && role.Has(menu.ID, Permission)) return;
            }
            else
            {
                XTrace.WriteLine("设计错误！验证权限时无法找到[{0}]的菜单", url);
            }

            var res = "[{0}/{1}] {2}".F(act.ControllerDescriptor.ControllerName, act.ActionName, menu != null ? (menu + "") : url);
            var msg = "访问资源 {0} 需要 {1} 权限".F(res, Permission.GetDescription());
            LogProvider.Provider.WriteLog("访问", "拒绝", msg);

            var vr = new ViewResult()
            {
                ViewName = "NoPermission"
            };
            vr.ViewBag.Context = filterContext;
            vr.ViewBag.Resource = res;
            vr.ViewBag.Permission = Permission;

            filterContext.Result = vr;
        }
        #endregion
    }
}