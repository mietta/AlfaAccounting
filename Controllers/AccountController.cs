﻿using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using AlfaAccounting.Models;
using System.Configuration;
using Microsoft.AspNet.Identity.EntityFramework;


namespace AlfaAccounting.Controllers
{
    /// <summary>
    /// Name:Mie Tanaka
    /// Name:26/05/2017
    /// Description: allows users log in and register their account
    /// </summary>
    
    [Authorize]
    public class AccountController : Controller
    {
        ApplicationDbContext db = new ApplicationDbContext();
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;
        

        /// <summary>
        /// default constructor of account controller
        /// </summary>
        public AccountController()
        {
        }

        /// <summary>
        /// Get user manager and signin mananger as parameter and 
        /// set it to the UsrManager and SignInManager property
        /// </summary>
        /// <param name="userManager"></param>
        /// <param name="signInManager"></param>
        public AccountController(ApplicationUserManager userManager, ApplicationSignInManager signInManager)
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }
        /// <summary>
        /// allow access to owing context throug signin manager
        /// </summary>
        public ApplicationSignInManager SignInManager
        {
            get
            {
                return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
            }
            private set
            {
                _signInManager = value;
            }
        }
        /// <summary>
        /// allows access to the ownin context through usermanager
        /// </summary>
        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

       
        /// <summary>
        /// returns empty log in view
        /// </summary>
        /// <param name="returnUrl"></param>
        /// <returns>returns empty log in view</returns>
        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            //Create the Admin account using setting in Web.config(if needed)
            CreateAdminIfNeeded();
            ViewBag.ReturnUrl = returnUrl;
            return View();
            //           return Redirect(ControllerContext.HttpContext.Request.UrlReferrer.ToString());
        }

        /// <summary>
        /// Checks input detail if passed model state is detail is valid,
        /// removes booking which does not have InvoiceId and its user id is same as logged in user.
        /// redirect user to privious url
        /// if does not have previous url returns Home/Index
        /// </summary>
        /// <param name="model"></param>
        /// <param name="returnUrl"></param>
        /// <returns>if model state is not valid returs current view else redirect user to privious url 
        /// if not revious url returns Home/Index</returns>
        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {

            if (!ModelState.IsValid)
            {
                return View(model);
            }
            //Require the user to have a confirmed email before they can login
            var user = await UserManager.FindByNameAsync(model.Email);
            if (user != null)
            {
                var loggedinuserid = user.Id;
                if (loggedinuserid != null)
                {
                    var tempbooking = db.Bookings.Where(b => b.InvoiceId == null && b.Id == loggedinuserid).ToList();
                    foreach (Booking item in tempbooking)
                    {
                        db.Bookings.Remove(item);
                        db.SaveChanges();
                    }
                }
                MigrateShoppingCart(user.Id);

                if (!await UserManager.IsEmailConfirmedAsync(user.Id))
                {
                    string callbackUrl = await SendEmailConfirmationTokenAsync(user.Id, "Confirm your account-Resend");

                    ViewBag.errorMessage = "You must have a confirmed email to log in";
                    return View("Error");
                }
            }

            // This doesn't count login failures towards account lockout
            // To enable password failures to trigger account lockout, change to shouldLockout: true
            var result = await SignInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, shouldLockout: false);
            switch (result)
            {
                case SignInStatus.Success:

                    return RedirectToLocal(returnUrl);/*RedirectToAction("Index","Home");*/
                case SignInStatus.LockedOut:
                    return View("Lockout");
                case SignInStatus.RequiresVerification:
                    return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = model.RememberMe });
                case SignInStatus.Failure:
                default:
                    ModelState.AddModelError("", "Invalid login attempt.");
                    return View(model);
            }
        }
        /// <summary>
        /// when user logged in shopping cart temp user id is given passed in user id
        /// </summary>
        /// <param name="Userid"></param>
        private void MigrateShoppingCart(string Userid)
        {
            // Associate shopping cart items with logged-in user
            var cart = ShoppingCart.GetCart(this.HttpContext);

            cart.MigrateCart(Userid);
            Session[ShoppingCart.CartSessionKey] = Userid;
        }



        //
        // GET: /Account/VerifyCode
        [AllowAnonymous]
        public async Task<ActionResult> VerifyCode(string provider, string returnUrl, bool rememberMe)
        {
            // Require that the user has already logged in via username/password or external login
            if (!await SignInManager.HasBeenVerifiedAsync())
            {
                return View("Error");
            }
            return View(new VerifyCodeViewModel { Provider = provider, ReturnUrl = returnUrl, RememberMe = rememberMe });
        }

        //
        // POST: /Account/VerifyCode
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> VerifyCode(VerifyCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // The following code protects for brute force attacks against the two factor codes. 
            // If a user enters incorrect codes for a specified amount of time then the user account 
            // will be locked out for a specified amount of time. 
            // You can configure the account lockout settings in IdentityConfig
            var result = await SignInManager.TwoFactorSignInAsync(model.Provider, model.Code, isPersistent: model.RememberMe, rememberBrowser: model.RememberBrowser);
            switch (result)
            {
                case SignInStatus.Success:
                    return RedirectToLocal(model.ReturnUrl);
                case SignInStatus.LockedOut:
                    return View("Lockout");
                case SignInStatus.Failure:
                default:
                    ModelState.AddModelError("", "Invalid code.");
                    return View(model);
            }
        }

        /// <summary>
        /// returns blank register view
        /// </summary>
        /// <returns>Register view</returns>
        // GET: /Account/Register
        [AllowAnonymous]
        public ActionResult Register()
        {
            return View();
        }

        /// <summary>
        /// allow user to register their detail
        /// send user email to verify their email address
        /// </summary>
        /// <param name="model"></param>
        /// <returns>Info view</returns>
        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    Companyname = model.Companyname,
                    Forename = model.Forename,
                    Surname = model.Surname,
                    Street = model.Street,
                    Town = model.Town,
                    Postcode = model.Postcode,
                    PhoneNumber = model.PhoneNumber,
                    UserName = model.Email,
                    Email = model.Email,
                };
                var result = await UserManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    //add user to the role to make User default
                    result = UserManager.AddToRole(user.Id, RoleName.ROLE_USER);

                    //Comment  the following line to prevent log in until the user is confirmed.
                    //await SignInManager.SignInAsync(user, isPersistent:false, rememberBrowser:false);

                    //enable twofactor
                    await UserManager.SetTwoFactorEnabledAsync(user.Id, true);

                    // For more information on how to enable account confirmation and password reset please visit http://go.microsoft.com/fwlink/?LinkID=320771
                    // Send an email with this link
                    string callbackUrl = await SendEmailConfirmationTokenAsync(user.Id, "Confirm your account");

                    ViewBag.Message = "Check your email and confirm your account to complete registration process, you must be confirmed before you can log in.. if you have not received the email check your junk mail box, in case you have input wrong email address and have not received email from us, you may need to register again";
                    return View("Info");
                    //   return RedirectToAction("Index", "Home");
                }
                AddErrors(result);
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }
        /// <summary>
        /// send email confirmation using passed userID and subject data
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="subject"></param>
        /// <returns></returns>
        private async Task<string> SendEmailConfirmationTokenAsync(string userID, string subject)
        {
            string code = await UserManager.GenerateEmailConfirmationTokenAsync(userID);
            var callbackUrl = Url.Action("ConfirmEmail", "Account",
               new { userId = userID, code = code }, protocol: Request.Url.Scheme);
            await UserManager.SendEmailAsync(userID,
               "Confirm your account", "Please confirm your account by clicking <a href=\""
               + callbackUrl + "\">here</a>");
            return callbackUrl;
        }


        // GET: /Account/ConfirmEmail
        [AllowAnonymous]
        public async Task<ActionResult> ConfirmEmail(string userId, string code)
        {
            if (userId == null || code == null)
            {
                return View("Error");
            }
            var result = await UserManager.ConfirmEmailAsync(userId, code);
            return View(result.Succeeded ? "ConfirmEmail" : "Error");
        }

        /// <summary>
        /// returns blank forgotpassword view 
        /// </summary>
        /// <returns>returns blank forgotpassword view</returns>
        // GET: /Account/ForgotPassword
        [AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        /// <summary>
        /// Gets posted model data, generate varification code and send it viea email
        /// </summary>
        /// <param name="model"></param>
        /// <returns>Forgotpassowrd confirmation view</returns>
        // POST: /Account/ForgotPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await UserManager.FindByNameAsync(model.Email);
                if (user == null || !(await UserManager.IsEmailConfirmedAsync(user.Id)))
                {
                    // if the user does not exist or is not confirmed, it does not reveal
                    return View("ForgotPasswordConfirmation");
                }

                // passward reset link get emailed 
                string code = await UserManager.GeneratePasswordResetTokenAsync(user.Id);
                var callbackUrl = Url.Action("ResetPassword", "Account", new { userId = user.Id, code = code }, protocol: Request.Url.Scheme);
                await UserManager.SendEmailAsync(user.Id, "Reset Password", "Please reset your password by clicking <a href=\"" + callbackUrl + "\">here</a>");
                return RedirectToAction("ForgotPasswordConfirmation", "Account");
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        /// <summary>
        /// Returns GorgotPasswordConfirmation View
        /// </summary>
        /// <returns>Returns GorgotPasswordConfirmation View</returns>
        // GET: /Account/ForgotPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        /// <summary>
        /// returns blank resest password view
        /// </summary>
        /// <param name="code"></param>
        /// <returns>returns blank resest password view</returns>
        // GET: /Account/ResetPassword
        [AllowAnonymous]
        public ActionResult ResetPassword(string code)
        {
            return code == null ? View("Error") : View();
        }


        // POST: /Account/ResetPassword
        /// <summary>
        /// Get post reset password data
        /// </summary>
        /// <param name="model"></param>
        /// <returns>returns reset password view</returns>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var user = await UserManager.FindByNameAsync(model.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return RedirectToAction("ResetPasswordConfirmation", "Account");
            }
            var result = await UserManager.ResetPasswordAsync(user.Id, model.Code, model.Password);
            if (result.Succeeded)
            {
                return RedirectToAction("ResetPasswordConfirmation", "Account");
            }
            AddErrors(result);
            return View();
        }


        // GET: /Account/ResetPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ResetPasswordConfirmation()
        {
            return View();
        }


        // POST: /Account/ExternalLogin
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ExternalLogin(string provider, string returnUrl)
        {
            // Request a redirect to the external login provider
            return new ChallengeResult(provider, Url.Action("ExternalLoginCallback", "Account", new { ReturnUrl = returnUrl }));
        }

        //
        // GET: /Account/SendCode
        [AllowAnonymous]
        public async Task<ActionResult> SendCode(string returnUrl, bool rememberMe)
        {
            var userId = await SignInManager.GetVerifiedUserIdAsync();
            if (userId == null)
            {
                return View("Error");
            }
            var userFactors = await UserManager.GetValidTwoFactorProvidersAsync(userId);
            var factorOptions = userFactors.Select(purpose => new SelectListItem { Text = purpose, Value = purpose }).ToList();
            return View(new SendCodeViewModel { Providers = factorOptions, ReturnUrl = returnUrl, RememberMe = rememberMe });
        }

        // POST: /Account/SendCode
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SendCode(SendCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View();
            }

            // Generate the token and send it
            if (!await SignInManager.SendTwoFactorCodeAsync(model.SelectedProvider))
            {
                return View("Error");
            }
            return RedirectToAction("VerifyCode", new { Provider = model.SelectedProvider, ReturnUrl = model.ReturnUrl, RememberMe = model.RememberMe });
        }

        //
        // GET: /Account/ExternalLoginCallback
        [AllowAnonymous]
        public async Task<ActionResult> ExternalLoginCallback(string returnUrl)
        {
            var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync();
            if (loginInfo == null)
            {
                return RedirectToAction("Login");
            }

            // Sign in the user with this external login provider if the user already has a login
            var result = await SignInManager.ExternalSignInAsync(loginInfo, isPersistent: false);
            switch (result)
            {
                case SignInStatus.Success:
                    return RedirectToLocal(returnUrl);
                case SignInStatus.LockedOut:
                    return View("Lockout");
                case SignInStatus.RequiresVerification:
                    return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = false });
                case SignInStatus.Failure:
                default:
                    // If the user does not have an account, then prompt the user to create an account
                    ViewBag.ReturnUrl = returnUrl;
                    ViewBag.LoginProvider = loginInfo.Login.LoginProvider;
                    return View("ExternalLoginConfirmation", new ExternalLoginConfirmationViewModel { Email = loginInfo.Email });
            }
        }

        //
        // POST: /Account/ExternalLoginConfirmation
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ExternalLoginConfirmation(ExternalLoginConfirmationViewModel model, string returnUrl)
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Manage");
            }

            if (ModelState.IsValid)
            {
                // Get the information about the user from the external login provider
                var info = await AuthenticationManager.GetExternalLoginInfoAsync();
                if (info == null)
                {
                    return View("ExternalLoginFailure");
                }
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
                var result = await UserManager.CreateAsync(user);
                if (result.Succeeded)
                {
                    result = await UserManager.AddLoginAsync(user.Id, info.Login);
                    if (result.Succeeded)
                    {
                        await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                        return RedirectToLocal(returnUrl);
                    }
                }
                AddErrors(result);
            }

            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }

        //
        // POST: /Account/LogOff
        /// <summary>
        /// deletes the bookings which does not have Invoice Id
        /// then logg off users
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            var loggedinuserid = User.Identity.GetUserId();
            var tempbooking = db.Bookings.Where(b => b.InvoiceId == null && b.Id == loggedinuserid).ToList();
            foreach (Booking item in tempbooking)
            {
                db.Bookings.Remove(item);
                db.SaveChanges();
            }
            return RedirectToAction("Index", "Home");
        }

        //
        // GET: /Account/ExternalLoginFailure
        [AllowAnonymous]
        public ActionResult ExternalLoginFailure()
        {
            return View();
        }




        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_userManager != null)
                {
                    _userManager.Dispose();
                    _userManager = null;
                }

                if (_signInManager != null)
                {
                    _signInManager.Dispose();
                    _signInManager = null;
                }
            }

            base.Dispose(disposing);
        }

        #region Helpers
        // Used for XSRF protection when adding external logins
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        internal class ChallengeResult : HttpUnauthorizedResult
        {
            public ChallengeResult(string provider, string redirectUri)
                : this(provider, redirectUri, null)
            {
            }

            public ChallengeResult(string provider, string redirectUri, string userId)
            {
                LoginProvider = provider;
                RedirectUri = redirectUri;
                UserId = userId;
            }

            public string LoginProvider { get; set; }
            public string RedirectUri { get; set; }
            public string UserId { get; set; }

            public override void ExecuteResult(ControllerContext context)
            {
                var properties = new AuthenticationProperties { RedirectUri = RedirectUri };
                if (UserId != null)
                {
                    properties.Dictionary[XsrfKey] = UserId;
                }
                context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
            }
        }
        #endregion

        // Utility

        // Add RoleManager
        /// <summary>
        /// Utitily method to allow access to RoleManager
        /// </summary>
        #region public ApplicationRoleManager RoleManager
        private ApplicationSignInManager.ApplicationRoleManager _roleManager;
        public ApplicationSignInManager.ApplicationRoleManager RoleManager
        {
            get
            {
                return _roleManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationSignInManager.ApplicationRoleManager>();
            }
            private set
            {
                _roleManager = value;
            }
        }
        #endregion

        // Add CreateAdminIfNeeded
        /// <summary>
        /// cretate AddminAccount specifed in web config if required.
        /// </summary>
        #region private void CreateAdminIfNeeded()
        private void CreateAdminIfNeeded()
        {
            // Get Admin Account
            string AdminUserName = ConfigurationManager.AppSettings["AdminUserName"];
            string AdminPassword = ConfigurationManager.AppSettings["AdminPassword"];
            string AdminCompanyname = ConfigurationManager.AppSettings["AdminCompanyName"];
            string AdminForename = ConfigurationManager.AppSettings["AdminForename"];
            string AdminSurname = ConfigurationManager.AppSettings["AdminSurname"];
            string AdminStreet = ConfigurationManager.AppSettings["AdminStreet"];
            string AdminPostcode = ConfigurationManager.AppSettings["AdminPostcode"];
            string AdminTown = ConfigurationManager.AppSettings["AdminTown"];
            string AdminPhoneNumber = ConfigurationManager.AppSettings["AdminPhoneNumber"];


            // See if Admin exists
            var objAdminUser = UserManager.FindByEmail(AdminUserName);

            if (objAdminUser == null)
            {
                //See if the Admin role exists
                if (!RoleManager.RoleExists("Administrator"))
                {
                    // Create the Admin Role (if needed)
                    IdentityRole objAdminRole = new IdentityRole("Administrator");
                    RoleManager.Create(objAdminRole);
                }

                // Create Admin user
                var objNewAdminUser = new ApplicationUser
                {
                    Companyname = AdminCompanyname,
                    Forename = AdminForename,
                    Surname = AdminSurname,
                    Street = AdminStreet,
                    Town = AdminTown,
                    Postcode = AdminPostcode,
                    PhoneNumber = AdminPhoneNumber,
                    UserName = AdminUserName,
                    Email = AdminUserName
                };
                var AdminUserCreateResult = UserManager.Create(objNewAdminUser, AdminPassword);
                // Put user in Admin role
                UserManager.AddToRole(objNewAdminUser.Id, "Administrator");
            }
        }
        #endregion
    }
}