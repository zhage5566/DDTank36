using System;
using System.Linq;
using Game.Logic;
using Game.Logic.Actions;
using Game.Logic.Phy.Object;
namespace Local36 {
 // Use the same Player.Shoot path as a player. BotShootAction directly calls
 // Living.ShootImp with a fixed bomb count and bypasses extra-shot semantics.
 public sealed class NativeBotShot36:BaseAction {
  readonly Player actor;Living target;readonly int turn,attempt;
  NativeBotShot36(Player player,Living aim,int turnIndex,int number,int delay):base(delay,0){actor=player;target=aim;turn=turnIndex;attempt=number;}
  public static void Queue(BaseGame game,Player actor,Living target){game.AddAction(new NativeBotShot36(actor,target,game.TurnIndex,0,900));}

  // The native solver returns the minimum-force (near-horizontal) arc, which clips
  // terrain between two stationary bots and explodes far from the target (0 damage).
  // Escalate the flight time (higher arc) until the trajectory clears the terrain.
  static int BlastRadius(Player actor){
   try{var ball=actor.CurrentBall;if(ball!=null){var p=ball.GetType().GetProperty("Radii",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance);if(p!=null)return Convert.ToInt32(p.GetValue(ball,null));}}catch{}
   return 120;
  }
  static bool PickShot(BaseGame game,Player actor,Living target,out int force,out int angle,out float time,out int spx,out int spy){
   force=0;angle=0;time=1.0f;spx=0;spy=0;
   var map=game.Map;if(map==null||actor.CurrentBall==null)return false;
   int near=BlastRadius(actor)+30;
   float[] times={1.0f,1.6f,2.2f,2.8f,3.4f};
   for(int i=0;i<times.Length;i++){
    int x=target.X,y=target.Y,f=0,a=0;
    actor.GetShootForceAndAngle(ref x,ref y,actor.CurrentBall.ID,1000,2500,actor.BallCount,times[i],ref f,ref a);
    if(f<=0||f>2000)continue;
    if(i==0){force=f;angle=a;spx=x;spy=y;} // remember minimum-arc fallback
    // Simulate the drag-free parabola this (force,angle) traces, self-calibrating
    // gravity from the native solution so it lands exactly on the target. Clear = it
    // reaches the target without first hitting terrain (or hits terrain near it).
    double rad=a*Math.PI/180.0;
    double vx0=f*Math.Cos(rad),vy0=f*Math.Sin(rad);
    double dx=target.X-x;if(Math.Abs(dx)<1||Math.Abs(vx0)<1){force=f;angle=a;time=times[i];spx=x;spy=y;return true;}
    double tstar=dx/vx0;if(tstar<=0){force=f;angle=a;time=times[i];spx=x;spy=y;return true;}
    double g=2.0*(target.Y-y-vy0*tstar)/(tstar*tstar);
    int steps=64;double dt=tstar/steps;double px=x,py=y,vx=vx0,vy=vy0;bool clear=true;
    for(int s=1;s<=steps;s++){
     vy+=g*dt;py+=vy*dt;px+=vx*dt;
     if(s<=2)continue; // spawns in open air at the muzzle
     int ix=(int)Math.Round(px),iy=(int)Math.Round(py);
     bool empty;try{empty=map.IsEmpty(ix,iy);}catch{continue;}
     if(!empty){
      double cdx=ix-target.X,cdy=iy-target.Y;
      clear=(cdx*cdx+cdy*cdy)<=(double)near*near;
      break;
     }
    }
    if(clear){force=f;angle=a;time=times[i];spx=x;spy=y;return true;}
   }
   return force>0&&force<=2000; // nothing cleared; keep the minimum-arc fallback
  }

  protected override void ExecuteImp(BaseGame game,long tick){try{
   if(game.GameState!=eGameState.Playing||game.TurnIndex!=turn||!actor.IsLiving||!actor.IsAttacking||actor.IsFrost)return;
   if(target==null||!target.IsLiving){
    if(game is PVPGame)target=game.GetAllLivingPlayers().Where(p=>p.Team!=actor.Team&&p.IsLiving).OrderBy(p=>Math.Abs(p.X-actor.X)).FirstOrDefault();
    else{var pve=game as PVEGame;target=game.FindAllNpcLiving().Cast<Living>().Concat(pve==null?Enumerable.Empty<Living>():pve.FindAllBoss().Cast<Living>()).Where(p=>p.IsLiving&&p.Team!=actor.Team).OrderBy(p=>Math.Abs(p.X-actor.X)).FirstOrDefault();}
   }
   if(target==null||actor.CurrentBall==null||attempt>=8){actor.SkipAttack();return;}
   actor.ChangeDirection(target.X>=actor.X?1:-1,100);
   int force,angle,spx,spy;float time;bool cleared=PickShot(game,actor,target,out force,out angle,out time,out spx,out spy);
   if(force<=0||force>2000){Console.WriteLine("BOT NATIVE AIM FAILED: "+actor.PlayerDetail.PlayerCharacter.ID);actor.SkipAttack();return;}
   int shots=actor.ShootCount,balls=actor.BallCount;bool fired=actor.Shoot(spx,spy,force,angle);
   Console.WriteLine("BOT NATIVE SHOT: id="+actor.PlayerDetail.PlayerCharacter.ID+" game="+game.Id+" turn="+turn+" shots="+shots+"->"+actor.ShootCount+" balls="+balls+" force="+force+" angle="+angle+" arcTime="+time+" cleared="+cleared+" fired="+fired);
   if(fired&&actor.IsLiving&&actor.IsAttacking&&actor.ShootCount>0)game.AddAction(new NativeBotShot36(actor,target,turn,attempt+1,1600));
   else if(!fired&&actor.IsAttacking)actor.SkipAttack();
  }catch(Exception e){Console.WriteLine("BOT NATIVE SHOT ERROR: "+e.GetType().Name);if(actor.IsAttacking)actor.SkipAttack();}finally{Finish(tick);}}
 }
}
