using System;
using System.Linq;
using System.Reflection;
using Game.Logic;
using Game.Logic.Actions;
using Game.Logic.Phy.Object;
using Game.Base.Packets;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
namespace Local36 {
 // Observe human claims unchanged. NPCs submit the same free native claim;
 // no entitlement, currency, drop table, probability or paid claim is modified.
 public static class NativeRewards36 {
  static Func<int,bool> IsBot;static bool installed;static NativeMethodHook36 hook;
  static Action<Game.Logic.Cmd.TakeCardCommand,BaseGame,Player,GSPacketIn> Original;
  sealed class WatchState{public DateTime Added=DateTime.UtcNow;public readonly System.Collections.Generic.HashSet<string> Claims=new System.Collections.Generic.HashSet<string>();}
  static readonly ConcurrentDictionary<BaseGame,WatchState> Games=new ConcurrentDictionary<BaseGame,WatchState>();static Timer timer;
  public static Action<BaseGame> Completed;
  public static void Install(Func<int,bool> bot){if(installed)return;installed=true;IsBot=bot;
   hook=new NativeMethodHook36(typeof(Game.Logic.Cmd.TakeCardCommand).GetMethod("HandleCommand"),typeof(NativeRewards36).GetMethod("Handle",BindingFlags.Static|BindingFlags.NonPublic));hook.Apply();Original=hook.Original<Action<Game.Logic.Cmd.TakeCardCommand,BaseGame,Player,GSPacketIn>>();timer=new Timer(Tick,null,1000,500);
  }
  public static void Observe(BaseGame game){if(game!=null){HitCredit36.Install();Games.GetOrAdd(game,g=>new WatchState());}}
  [MethodImpl(MethodImplOptions.NoInlining)]static void Handle(Game.Logic.Cmd.TakeCardCommand handler,BaseGame game,Player actor,GSPacketIn packet){BeforeClaim(game,actor,packet);Original(handler,game,actor,packet);AfterClaim(game,actor);}
  static void Tick(object unused){foreach(var pair in Games){try{var game=pair.Key;var state=pair.Value;
   // ALLSessionStopped is PVE's final card window (native WaitTime 21000),
   // not room disposal. Keep participants and free native claims alive until
   // the actual Stopped state/GameStopped event.
   if(game.GameState==eGameState.Stopped||(DateTime.UtcNow-state.Added).TotalMinutes>30){WatchState old;if(Games.TryRemove(game,out old)&&game.GameState==eGameState.Stopped){var completed=Completed;if(completed!=null)completed(game);}continue;}
   if(game.GameState!=eGameState.GameOver&&game.GameState!=eGameState.ALLSessionStopped)continue;
   foreach(var player in game.GetAllPlayers()){string key=game.GameState+"/"+(game is PVEGame?((PVEGame)game).SessionId:0)+"/"+player.PlayerDetail.PlayerCharacter.ID;
    if(player.CanTakeOut<=0||player.FinishTakeCard)continue;lock(state.Claims){if(!state.Claims.Add(key))continue;}
    Console.WriteLine("NATIVE CARD ENTITLEMENT: game="+game.Id+" user="+player.PlayerDetail.PlayerCharacter.ID+" state="+game.GameState+" eligible="+player.CanTakeOut+" finished="+player.FinishTakeCard);
    if(IsBot(player.PlayerDetail.PlayerCharacter.ID)&&player.CanTakeOut>0&&!player.FinishTakeCard)game.AddAction(new Claim(player));
   }
  }catch(Exception e){Console.WriteLine("NATIVE CARD OBSERVE: "+e.GetType().Name);}}}
  static void BeforeClaim(BaseGame __0,Player __1,GSPacketIn __2){Console.WriteLine("NATIVE CARD REQUEST: game="+__0.Id+" user="+__1.PlayerDetail.PlayerCharacter.ID+" state="+__0.GameState+" eligible="+__1.CanTakeOut+" finished="+__1.FinishTakeCard+" cursor="+__2.Offset);}
  static void AfterClaim(BaseGame __0,Player __1){Console.WriteLine("NATIVE CARD RESULT: game="+__0.Id+" user="+__1.PlayerDetail.PlayerCharacter.ID+" remaining="+__1.CanTakeOut+" finished="+__1.FinishTakeCard);}
  sealed class Claim:BaseAction {readonly Player player;readonly int budget;public Claim(Player actor,int remaining=8):base(1800,0){player=actor;budget=remaining;}
   protected override void ExecuteImp(BaseGame game,long tick){try{if(game.GameState==eGameState.Playing||game.GameState==eGameState.SessionPrepared)return;
    if(player.CanTakeOut>0&&!player.FinishTakeCard){bool taken=game.TakeCard(player,false);Console.WriteLine("BOT NATIVE CARD: game="+game.Id+" user="+player.PlayerDetail.PlayerCharacter.ID+" taken="+taken+" remaining="+player.CanTakeOut+" finished="+player.FinishTakeCard);if(taken&&player.CanTakeOut>0&&!player.FinishTakeCard&&budget>1)game.AddAction(new Claim(player,budget-1));}
   }catch(Exception e){Console.WriteLine("BOT NATIVE CARD ERROR: "+e.GetBaseException().Message);}finally{Finish(tick);}}
  }
 }
}
