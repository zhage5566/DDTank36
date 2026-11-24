<%@ Application Language="C#" %>

<script runat="server">

    void Application_Start(object sender, EventArgs e)
    {
        // 在应用程序启动时运行的代码

    }

    void Application_End(object sender, EventArgs e)
    {
        //  在应用程序关闭时运行的代码
    }

    protected void Application_Error(Object sender, EventArgs e)
    {

        //错误的全局处理 
        Exception ex = Server.GetLastError().GetBaseException();
        //记录错误日志
        string strFile = Request.ApplicationPath + "/errorlog/" + DateTime.Today.ToShortDateString() + "sys_error.log";

        strFile = Server.MapPath(strFile);
        System.IO.StreamWriter st = new System.IO.StreamWriter(strFile, true, System.Text.Encoding.UTF8);

        string sText = "";
        sText += "\r\n//----------------------------------------------------------------------------\r\n";
        sText += "//----------------------------------------------------------------------------\r\n";
        sText += "系统出现如下错误：\r\n";
        sText += "　　　　发生时间  ：  " + DateTime.Now.ToString() + "\r\n";
        sText += "　　　　错误描述  ：  " + ex.Message.Replace("\r\n", "") + "\r\n";
        sText += "　　　　错误对象  ：  " + ex.Source + "\r\n";
        sText += "　　　　错误帮助  ：  " + ex.HelpLink + "\r\n";
        sText += "　　　　错误页面  ：  " + Request.UrlReferrer + "\r\n";        
        if (Request.UrlReferrer == null)
        {
            sText += "　　　　来源页面  ：  直接输入\r\n";
        }
        else
        {
            sText += "　　　　来源页面  ：  " +Request.UrlReferrer.ToString() + "\r\n";
        }
        
        sText += "　　　　用户  IP  ：  " + Request.UserHostAddress + "\r\n";
        sText += "　　　　用户  IE  ：  " + Request.UserAgent + "\r\n";
        sText += "　　　　详细描述  ：\r\n" + ex.StackTrace + "\r\n";
        sText += "\r\n";
        st.Write(sText);
        st.Close();

        string ErrMsg = ex.Message;

        string code = "110";

        if (ErrMsg.IndexOf("不存在", 1) >= 1)                       //访问当前页面不存在
        {
            code = "0";
        }
        if (ErrMsg.IndexOf("未将对象", 1) >= 1)                       //访问当前页面不存在
        {
            code = "1";
        }

        if (ErrMsg.IndexOf("的定义", 1) >= 1)                 //程序发生了逻辑错误
        {
            code = "2";
        }
        if (ErrMsg.IndexOf("不存在名称", 1) >= 1)//程序发生了逻辑错误
        {
            code = "3";
        }
        if (ErrMsg.IndexOf("未找到", 1) >= 1)//未找到
        {
            code = "6";
        }
        if (Request.ApplicationPath == "/")//以网站方式访问
        {
            Response.Redirect(Request.ApplicationPath + "err.html?code=" + code);
        }
        else                              //以虚拟目录访问
        {
            Response.Redirect(Request.ApplicationPath + "/err.html?code=" + code);
        }

    }

    void Session_Start(object sender, EventArgs e)
    {
 
        
    }

    void Session_End(object sender, EventArgs e)
    {
       
        
    }
       
</script>
