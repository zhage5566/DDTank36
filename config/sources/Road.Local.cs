using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Data.SqlClient;
using System.Configuration;
using HarmonyLib;
using System.Collections.Generic;

// Local development host: invoke the original game server directly.
// The original server assembly, game protocols and account checks are unchanged.
internal static class LocalRoadHost {
    private static readonly HashSet<string> RoutedConnections=new HashSet<string>();
    private static void BeforeSqlOpen(SqlConnection __instance) {
        string host=__instance.DataSource;
        string database=__instance.Database;
        bool legacyHost=host=="." || host=="(local)" || host=="localhost" || host=="127.0.0.1" || host=="192.168.1.202";
        if(!legacyHost)return;
        string target=database;
        if(target=="Db_Tank")target="Db_Tank_S1";
        if(target!="Db_Tank_S1" && target!="Db_Membership" && target!="Db_Count" && target!="Db_Cross" && target!="Db_Logs")return;
        var settings=new SqlConnectionStringBuilder(ConfigurationManager.AppSettings["conString"]);
        settings.InitialCatalog=target;
        __instance.ConnectionString=settings.ConnectionString;
        lock(RoutedConnections)if(RoutedConnections.Add(host+"/"+database))Console.WriteLine("Routed legacy database connection to configured instance: "+host+"/"+target);
    }
    private static int Main(string[] args) {
        string folder=AppDomain.CurrentDomain.BaseDirectory;
        Directory.SetCurrentDirectory(folder);
        AppDomain.CurrentDomain.AssemblyResolve += (sender,eventArgs) => {
            string name=new AssemblyName(eventArgs.Name).Name;
            string path=Path.Combine(folder,name+".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        try {
            Console.WriteLine("DDTank 3.6 local development console host");
            var harmony=new Harmony("ddtank36.local.database.compatibility");
            harmony.Patch(typeof(SqlConnection).GetMethod("Open",Type.EmptyTypes),prefix:new HarmonyMethod(typeof(LocalRoadHost).GetMethod("BeforeSqlOpen",BindingFlags.NonPublic|BindingFlags.Static)));
            Assembly server=Assembly.LoadFrom(Path.Combine(folder,"Road.Service.exe"));
            if(args.Length==2 && args[0]=="--probe-player") {
                Type businessType=server.GetType("Bussiness.PlayerBussiness",true);
                object business=Activator.CreateInstance(businessType);
                try {
                    object player=businessType.GetMethod("GetUserSingleByUserID",new Type[]{typeof(int)}).Invoke(business,new object[]{Int32.Parse(args[1])});
                    if(player==null)throw new InvalidOperationException("Player initialization returned null.");
                    Console.WriteLine("Player initialization succeeded for ID "+args[1]);
                    foreach(string field in new string[]{"ID","UserID","NickName","Grade","GP"}) {
                        var info=player.GetType().GetProperty(field);
                        if(info!=null)Console.WriteLine(field+"="+info.GetValue(player,null));
                    }
                    return 0;
                }finally{var disposable=business as IDisposable;if(disposable!=null)disposable.Dispose();}
            }
            Type configType=server.GetType("Game.Server.GameServerConfig",true);
            object config=Activator.CreateInstance(configType);
            Type serverType=server.GetType("Game.Server.GameServer",true);
            serverType.GetMethod("CreateInstance",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{config});
            object instance=serverType.GetProperty("Instance",BindingFlags.Public|BindingFlags.Static).GetValue(null,null);
            Assembly bots=Assembly.LoadFrom(Path.Combine(folder,"Local36.Bots.dll"));
            object botRoot=bots.GetType("Local36.Paths36",true).GetField("Root",BindingFlags.Public|BindingFlags.Static).GetValue(null);
            bots.GetType("Local36.CombatDamage34",true).GetMethod("Configure",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{botRoot});
            foreach(string rules in new[]{"BattleTransport36","StrictDerived34","CombatDamage34","DerivedProfile34"})bots.GetType("Local36."+rules,true).GetMethod("Install").Invoke(null,null);
            bots.GetType("Local36.TakeDamageFix36",true).GetMethod("Install",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{harmony});
            bots.GetType("Local36.DamageReceiveAudit36",true).GetMethod("Install",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{harmony,server,botRoot});
            bool started=(bool)serverType.GetMethod("Start",Type.EmptyTypes).Invoke(instance,null);
            if(!started)throw new InvalidOperationException("Original GameServer.Start returned false; see server logs.");
            Console.WriteLine("Original GameServer.Start succeeded.");
            bots.GetType("Local36.BotService",true).GetMethod("Start").Invoke(null,null);
            AppDomain.CurrentDomain.ProcessExit += (sender,eventArgs) => {
                try{serverType.GetMethod("Stop",Type.EmptyTypes).Invoke(instance,null);}catch{}
            };
            Thread.Sleep(Timeout.Infinite);
            return 0;
        } catch(Exception exception) {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
