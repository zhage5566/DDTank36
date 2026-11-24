using System;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Services;
using System.Web.Services.Protocols;
using System.Xml.Linq;

using System.Web.Security;
using Membership_DAL;

namespace DanDanTang.Flash_Port
{
    /// <summary>
    /// Summary description for PassPort
    /// </summary>
    [WebService(Namespace = "dandantang")]

    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    // [System.Web.Script.Services.ScriptService]
    public class PassPort : System.Web.Services.WebService
    {
        IMem_UserInfo Iuserinfo = new IMem_UserInfo();
        [WebMethod]
        public string ChenckValidate(string applicationname,string username, string password)
        {
            int? passwordformat = 0;
            string passwordsalt = "";
            string enpassword = "";
            int? userid = 0;
            Iuserinfo.GetAddPasswordWay(applicationname,username, ref passwordformat, ref passwordsalt);
            switch (passwordformat)
            {
                case 0://未加密
                    enpassword = password;
                    Iuserinfo.ValidateUser(applicationname,username, enpassword, ref userid);
                    break;
                case 1://采用MD5加密
                    enpassword = FormsAuthentication.HashPasswordForStoringInConfigFile(password, "md5");
                    Iuserinfo.ValidateUser(applicationname,username, enpassword, ref userid);
                    break;
                default:
                    userid = 0;
                    break;

            }
            return userid.ToString();

        }
        [WebMethod]
        public bool? Get_UserSex(string applicationname,string username)
        {
            return Iuserinfo.FindUserSex(applicationname,username);
        }

    }


}
