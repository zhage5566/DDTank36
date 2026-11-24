using System;
using System.Collections.Generic;
using System.Drawing;
using Game.Base.Packets;
using Game.Logic;
using Game.Logic.Cmd;
using Game.Logic.Phy.Object;
using Bussiness.Managers;
using SqlDataProvider.Data;

namespace Local36 {
 public sealed class BattleTacticsResult36 {
  public int initialEnergy,remainingEnergy,usedEnergy,moved,moveCommands,blockedMoves,xBefore,yBefore,xAfter,yAfter;
  public bool shotReady,stuntUsed;public int danderBefore,danderAfter;
  public string stopReason,plan,movement;
  public int shootCount,ballCount;
  public List<int> props=new List<int>();
 }
 // Compile this source separately against either same-package Road or Fight.
 // It is a pre-shot helper: the caller still queues native BotCommand/
 // BotShootAction afterwards, so native ballistic angle/force remain authoritative.
 public static class BattleTactics36 {
  const int MaxMoveCommands=160,MaxPathPixels=1024,LocalRadius=128,MaxSlope=6;
  static readonly int[] DamageProps={10004,10005,10006,10007,10008};
  public static BattleTacticsResult36 Prepare(BaseGame game,Player actor,Living target){
   var result=new BattleTacticsResult36();
   if(game==null||actor==null||target==null){result.stopReason="missing_actor_or_target";return result;}
   result.initialEnergy=actor.Energy;result.xBefore=actor.X;result.yBefore=actor.Y;
   try{
    if(!Ready(game,actor)){result.stopReason="not_an_active_shooting_turn";return Finish(actor,result);}
    if(actor.CurrentBall==null){result.stopReason="no_native_ball";return Finish(actor,result);}
    result.danderBefore=actor.Dander;
    if(actor.Dander>=200&&!actor.GetSealState()&&actor.PetEffects.CurrentUseSkill==0){var stunt=new GSPacketIn(91,actor.PlayerDetail.PlayerCharacter.ID);stunt.WriteByte(15);stunt.WriteByte(0);stunt.WriteHeader();stunt.Offset=GSPacketIn.HDR_SIZE+1;new StuntCommand().HandleCommand(game,actor,stunt);result.stuntUsed=actor.Dander<result.danderBefore;}
    result.danderAfter=actor.Dander;
    // Give the turn a genuine movement phase before selecting props. Each short
    // route is checked column by column, and only the native command mutates state.
    int homeX=actor.X;uint choice=unchecked((uint)(actor.PlayerDetail.PlayerCharacter.ID*37+game.TurnIndex*13+game.Id*7));
    int variant=(int)(choice%5),movement=(int)((choice/5)%3);result.movement=movement==0?"stand":movement==1?"move_before_props":"move_after_props";
    int[][] plans={new[]{10002},new[]{10001},new[]{10003,10001},new[]{10003,10002},new int[0]};
    result.plan=new[]{"extra_one_and_damage","extra_two_and_damage","trident_extra_two","trident_extra_one","full_damage"}[variant];
    if(movement==1)Walk(game,actor,target,homeX,24,result);
    if(Ready(game,actor)&&!actor.GetSealState()){
     foreach(int id in plans[variant]){var planned=ItemMgr.FindItemTemplate(id);if(LegalProp(actor,planned)&&!UseProp(game,actor,planned,result))break;}
     // Fill the remaining legal budget with built-in damage props. Extra shots
     // and trident use native effects and are executed by NativeBotShot36.
     for(int count=0;count<8&&Ready(game,actor);count++){
      ItemTemplateInfo item=BestDamage(actor);
      if(item==null)break;
      if(!UseProp(game,actor,item,result))break;
     }
    }
    if(movement!=0&&Ready(game,actor)&&actor.Energy>0)Walk(game,actor,target,homeX,MaxPathPixels-result.moved,result);
    if(Ready(game,actor))actor.ChangeDirection(target.X>=actor.X?1:-1,100);
    if(actor.Energy==0)result.stopReason="energy_spent";
    else if(!Ready(game,actor))result.stopReason="native_turn_changed";
    else if(movement==0)result.stopReason="standing_below_legal_prop_cost";
    else if(String.IsNullOrEmpty(result.stopReason))result.stopReason="no_further_legal_action";
   }catch(Exception e){result.stopReason="native_tactics_error:"+e.GetType().Name;}
   return Finish(actor,result);
  }
  static BattleTacticsResult36 Finish(Player actor,BattleTacticsResult36 r){
   r.remainingEnergy=actor.Energy;r.usedEnergy=Math.Max(0,r.initialEnergy-r.remainingEnergy);r.xAfter=actor.X;r.yAfter=actor.Y;
   r.shotReady=actor.IsLiving&&actor.IsAttacking&&!actor.IsFrost&&actor.CurrentBall!=null;r.shootCount=actor.ShootCount;r.ballCount=actor.BallCount;
   return r;
  }
  static bool Ready(BaseGame game,Player actor){return game.GameState==eGameState.Playing&&actor.IsLiving&&actor.IsAttacking&&!actor.IsFrost;}
  static bool LegalProp(Player actor,ItemTemplateInfo item){return item!=null&&(item.Property1==13||item.Property1==14||item.Property1==15)&&item.Property2>0&&item.Property4>0&&item.Property4<=actor.Energy&&item.Property6!=1&&actor.CanUseItem(item);}
  static bool UseProp(BaseGame game,Player actor,ItemTemplateInfo item,BattleTacticsResult36 result){
   if(!Ready(game,actor)||!LegalProp(actor,item))return false;int energy=actor.Energy,count=actor.Prop==null?0:actor.Prop.Count;
   var packet=new GSPacketIn(91,actor.PlayerDetail.PlayerCharacter.ID);packet.WriteByte(32);packet.WriteByte(1);packet.WriteInt(item.TemplateID-10001);packet.WriteInt(item.TemplateID);packet.WriteHeader();packet.Offset=GSPacketIn.HDR_SIZE+1;new PropUseCommand().HandleCommand(game,actor,packet);
   if(actor.Energy>=energy||actor.Prop==null||actor.Prop.Count<=count){result.stopReason="native_prop_refused";return false;}result.props.Add(item.TemplateID);return true;
  }
  static ItemTemplateInfo BestDamage(Player actor){
   // Never rely on IsAutoBot's native CanUseItem shortcut to justify overspend.
   // Property6=1 would end the turn; disallow it even if a template is edited.
   foreach(int id in DamageProps){
    var item=ItemMgr.FindItemTemplate(id);
    if(item!=null&&item.Property1==13&&item.Property2>0&&item.Property4>0&&item.Property4<=actor.Energy&&item.Property6!=1&&actor.CanUseItem(item))return item;
   }
   return null;
  }
  static void Walk(BaseGame game,Player actor,Living target,int homeX,int allowance,BattleTacticsResult36 r){
   if(allowance<=0||actor.Energy<=0)return;
   if(actor.LockMove||actor.isLockXY){r.stopReason="native_movement_locked";return;}
   if(actor.LimitEnergy||actor.isLockEmery){r.stopReason="native_movement_energy_locked";return;}
   if(actor.PowerRatio<=0){r.stopReason="native_free_movement";return;}
   if(game.Map==null){r.stopReason="no_native_map";return;}
   Rectangle bounds=game.Map.Bound;
   if(bounds.Width<64||bounds.Height<48){r.stopReason="map_has_no_safe_margin";return;}
   int pixels=0,preferred=PreferredDirection(actor,target);
   while(Ready(game,actor)&&actor.Energy>0&&pixels<allowance&&r.moveCommands<MaxMoveCommands&&r.moved<MaxPathPixels){
    int minimumStep=(int)Math.Max(1,(100L+actor.PowerRatio-1)/actor.PowerRatio);
    int step=Math.Min(Math.Max(8,minimumStep),Math.Min(allowance-pixels,MaxPathPixels-r.moved));
    // SetXY's actual native cost is floor(abs(dx)*PowerRatio/100).
    while(step>0&&(long)step*actor.PowerRatio/100>actor.Energy)step--;
    if(step<=0||(long)step*actor.PowerRatio/100<=0){r.stopReason="below_minimum_native_step_cost";break;}
    Point next=Point.Empty;int direction=preferred;bool route=false;
    // A failed eight-pixel segment must not hide a shorter legal route or the
    // final one-energy remainder. Search bounded shorter segments both ways.
    for(int candidate=step;candidate>0&&!route;candidate--){
     if((long)candidate*actor.PowerRatio/100<=0)continue;
     direction=preferred;route=SafeRoute(game,actor,target,homeX,direction,candidate,out next);
     if(!route){r.blockedMoves++;direction=-direction;route=SafeRoute(game,actor,target,homeX,direction,candidate,out next);}
     if(!route)r.blockedMoves++;
    }
    if(!route){r.stopReason="no_safe_ground_route";break;}
    preferred=direction;
    int oldX=actor.X,oldY=actor.Y,before=actor.Energy;
    var packet=new GSPacketIn(91,actor.PlayerDetail.PlayerCharacter.ID);
    packet.WriteByte(9);packet.WriteByte(0);packet.WriteInt(next.X);packet.WriteInt(next.Y);packet.WriteByte(direction<0?(byte)255:(byte)1);packet.WriteBoolean(true);packet.WriteShort((short)game.TurnIndex);
    // Native MoveStartCommand clones and reads the wire header before broadcast.
    packet.WriteHeader();packet.Offset=GSPacketIn.HDR_SIZE+1;
    new MoveStartCommand().HandleCommand(game,actor,packet);
    r.moveCommands++;
    int delta=Math.Abs(actor.X-oldX);r.moved+=delta;pixels+=delta;
    if(actor.X==oldX&&actor.Y==oldY){r.stopReason="native_move_refused";break;}
    if(actor.Energy>=before){r.stopReason="native_move_did_not_spend_energy";break;}
    // The native fall solver should agree with the validated ground point.
    // If it does not, stop rather than compensating by writing coordinates.
    if(!actor.IsLiving||Math.Abs(actor.Y-next.Y)>MaxSlope){r.stopReason="native_ground_changed";break;}
    if(Math.Abs(actor.X-homeX)>=LocalRadius-8)preferred=-direction;
   }
   if(r.moveCommands>=MaxMoveCommands||r.moved>=MaxPathPixels)r.stopReason="bounded_native_action_budget";
  }
  static int PreferredDirection(Player actor,Living target){
   int toward=target.X>=actor.X?1:-1;
   // Keep blast separation at close range; otherwise advance toward the opponent.
   return Math.Abs(target.X-actor.X)<180?-toward:toward;
  }
  static bool SafeRoute(BaseGame game,Player actor,Living target,int homeX,int direction,int step,out Point destination){
   destination=Point.Empty;int x=actor.X,y=actor.Y;Rectangle map=game.Map.Bound;
   for(int i=0;i<step;i++){
    Point p=game.Map.FindNextWalkPoint(x,y,direction,1,MaxSlope);
    if(p.IsEmpty||p.X!=x+direction||Math.Abs(p.Y-y)>MaxSlope)return false;
    if(p.X<map.Left+24||p.X>=map.Right-24||p.Y<map.Top+24||p.Y>=map.Bottom-24||Math.Abs(p.X-homeX)>LocalRadius)return false;
    // No walking through an opponent or using an edge approach as an energy sink.
    if(Math.Abs(p.X-target.X)<64&&Math.Abs(p.Y-target.Y)<64)return false;
    // Physics.Bound is the relative m_rect (confirmed normal CIL), not a world
    // rectangle. Offset by the candidate world position exactly once.
    Rectangle body=actor.Bound;body.Offset(p.X,p.Y);body.Inflate(-2,-2);
    // Native Player's hit rectangle is (-15,-20,30,30), extending ten pixels
    // below its ground anchor. Test walking clearance only above that anchor;
    // using the entire damage-hit box would reject even a flat floor.
    if(body.Bottom>p.Y-2)body.Height=p.Y-2-body.Top;
    if(body.Width<=0||body.Height<=0||!game.Map.IsRectangleEmpty(body))return false;
    // Native Tile.IsRectangleEmptyQuick checks only four corners. Sample the
    // interior too, otherwise a low ceiling between those corners is missed.
    for(int bx=body.Left;bx<=body.Right;bx+=3)for(int by=body.Top;by<=body.Bottom;by+=3)if(!game.Map.IsEmpty(bx,by))return false;
    destination=p;x=p.X;y=p.Y;
   }
   return !destination.IsEmpty;
  }
 }
}
