using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Threading;
using Game.Logic;
using Game.Logic.Actions;
using Game.Logic.Phy.Object;
namespace Local36 {
 // Called by both the Road local-game driver and the Fight turn notification.
 // Every mutation runs on the game's action queue; one plan per actor/native turn.
 public static class NativeBotTurn36 {
  sealed class Cursor {public BaseGame Game;public int Turn=-1,Session=-1,Recovery;public DateTime Deadline;}
  static readonly ConditionalWeakTable<Player,Cursor> Turns=new ConditionalWeakTable<Player,Cursor>();
  static readonly ConcurrentDictionary<Player,Cursor> Active=new ConcurrentDictionary<Player,Cursor>();
  static readonly Timer Watch=new Timer(Check,null,1000,1000);
  public static Action<Player,BattleTacticsResult36> Prepared;public static Func<int,bool> IsResidentBot;
  public static bool Queue(BaseGame game,Player actor,Living target){
   if(game==null||actor==null||game.GameState!=eGameState.Playing||!actor.IsLiving||!actor.IsAttacking)return false;
   NativeRewards36.Observe(game);
   int session=game is PVEGame?((PVEGame)game).SessionId:0;var cursor=Turns.GetOrCreateValue(actor);
   lock(cursor){if(Object.ReferenceEquals(cursor.Game,game)&&cursor.Turn==game.TurnIndex&&cursor.Session==session)return false;
    cursor.Game=game;cursor.Turn=game.TurnIndex;cursor.Session=session;cursor.Recovery=0;cursor.Deadline=DateTime.UtcNow.AddSeconds(25);
    int delay=350;var resident=IsResidentBot;if(game.TurnIndex<=1&&resident!=null&&game.GetAllPlayers().Any(p=>!resident(p.PlayerDetail.PlayerCharacter.ID)))delay=8000;
    try{game.AddAction(new TurnAction(game,actor,target,false,delay));Active[actor]=cursor;return true;}
    catch{cursor.Turn=-1;throw;}
   }
  }
  // A delayed recovery action sitting in BaseGame's queue prevents its native
  // "all actions finished" test, delaying every normal turn. Observe outside the
  // queue, and enqueue recovery only after a real timeout has occurred.
  static void Check(object unused){foreach(var pair in Active){try{var actor=pair.Key;var state=pair.Value;lock(state){var game=state.Game;
   if(game.GameState!=eGameState.Playing||game.TurnIndex!=state.Turn||(game is PVEGame&&((PVEGame)game).SessionId!=state.Session)||!actor.IsLiving||!actor.IsAttacking){Cursor old;Active.TryRemove(actor,out old);continue;}
   if(DateTime.UtcNow>=state.Deadline&&Interlocked.CompareExchange(ref state.Recovery,1,0)==0)game.AddAction(new TurnAction(game,actor,null,true,0));
  }}catch(Exception e){Console.WriteLine("BOT WATCHDOG: "+e.GetType().Name);}}}
  sealed class TurnAction:BaseAction {
   readonly Player actor;readonly Living target;readonly BaseGame owner;readonly int turn,session;readonly bool watchdog;
   public TurnAction(BaseGame game,Player player,Living aim,bool recovery,int delay):base(delay,0){owner=game;actor=player;target=aim;turn=game.TurnIndex;session=game is PVEGame?((PVEGame)game).SessionId:0;watchdog=recovery;}
   protected override void ExecuteImp(BaseGame game,long tick){try{
    if(!Object.ReferenceEquals(game,owner)||game.GameState!=eGameState.Playing||game.TurnIndex!=turn||(game is PVEGame&&((PVEGame)game).SessionId!=session)||!actor.IsLiving||!actor.IsAttacking)return;
    if(watchdog){Console.WriteLine("BOT TURN RECOVERY: id="+actor.PlayerDetail.PlayerCharacter.ID+" game="+game.Id+" turn="+turn);actor.SkipAttack();return;}
    if(actor.IsFrost||target==null||!target.IsLiving){actor.SkipAttack();return;}
    var tactics=BattleTactics36.Prepare(game,actor,target);
    var observer=Prepared;if(observer!=null)observer(actor,tactics);
    Console.WriteLine("BOT TACTICS: "+actor.PlayerDetail.PlayerCharacter.ID+" "+new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(tactics));
    if(!tactics.shotReady){if(actor.IsAttacking)actor.SkipAttack();return;}
    NativeBotShot36.Queue(game,actor,target);
   }catch(Exception e){Console.WriteLine("BOT TURN ERROR: "+e.GetType().Name);if(actor.IsAttacking)actor.SkipAttack();}finally{Finish(tick);}}
  }
 }
}
