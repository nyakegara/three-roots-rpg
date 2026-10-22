using System.Text.Json;
using Game.Core.Combat;
using Game.Core.Combat.AI;
using Game.Core.Combat.Generation;
using Game.Core.Content;
using Game.Core.Geometry;

var root=Path.GetFullPath(args.ElementAtOrDefault(0)??".");
var samples=int.TryParse(args.ElementAtOrDefault(1),out var requested)?requested:50;
var terrain=args.ElementAtOrDefault(2)=="generated"?"generated":"flat";
var disableConvert=args.Contains("no-convert");
var disableHeal=args.Contains("no-heal");
var floraCostMultiplier=args.FirstOrDefault(arg=>arg.StartsWith("flora-cost=",StringComparison.Ordinal)) is { } floraCostArg
    ?double.Parse(floraCostArg[11..],System.Globalization.CultureInfo.InvariantCulture):1.0;
var loaded=UnitCatalog.Load(Path.Combine(root,"content/units/units.json"));
if(!loaded.Success)throw new Exception(string.Join("; ",loaded.Errors.Select(error=>error.Message)));
var units=loaded.Catalog!.Units;
var pairings=new[]{("flora","human"),("flora","undead"),("human","undead")};
var budgets=new[]{300,600,900};
var results=new List<object>();
foreach(var (first,second) in pairings)foreach(var budget in budgets){
 int firstWins=0,secondWins=0,draws=0;
 long firstSurvivors=0,secondSurvivors=0;
 for(int seed=0;seed<samples;seed++)for(int swap=0;swap<2;swap++){
  // Stable across processes; String.GetHashCode is deliberately randomized by .NET.
  var rng=new Random(seed*104729+budget*31+first[0]*7+second[0]);
  var firstArmy=CreateArmy(first,budget,rng);var secondArmy=CreateArmy(second,budget,rng);
  var left=swap==0?firstArmy:secondArmy;var right=swap==0?secondArmy:firstArmy;
  var map=terrain=="generated"?new BattlefieldGenerator().Generate(seed*23+budget).Map:
      new BattleMap(from q in Enumerable.Range(0,9) from r in Enumerable.Range(0,7)
                    select new BattleTile(new HexCoord(q,r)));
  var troops=new List<BattleUnit>();
  for(int i=0;i<5;i++){
   var row=i+1;
   troops.Add(Create(left[i],BattleSide.Player,terrain=="generated"?new(2-row/2,row):new(1,row),$"L{i}"));
   troops.Add(Create(right[i],BattleSide.Enemy,terrain=="generated"?new(9-row/2,row):new(7,row),$"R{i}"));
  }
  var battle=new Battle(map,troops,new Random(seed));var ai=new BattleAi();int actions=0;
  while(!battle.IsFinished&&battle.Round<35&&actions++<3000){
   if(battle.ActiveUnit is null){battle.StartRound();continue;}
   var actor=battle.ActiveUnit;var action=ai.ChooseAction(battle,actor.Id);
   bool applied=action.Type switch{
    BattleActionType.Attack=>battle.Attack(actor.Id,action.TargetId!),
    BattleActionType.Heal=>battle.TryHeal(actor.Id,action.TargetId!),
    BattleActionType.Move=>battle.Move(actor.Id,action.Destination!.Value),
    BattleActionType.SetAttackMode=>battle.TrySetAttackMode(actor.Id,action.Mode!.Value),
    _=>false};
   if(!applied)battle.EndActivation();
  }
  var actual=battle.Winner;
  var winner=actual is null?null:swap==0
    ?actual==BattleSide.Player?first:second
    :actual==BattleSide.Player?second:first;
  if(winner==first)firstWins++;else if(winner==second)secondWins++;else draws++;
  foreach(var troop in battle.Units.Where(t=>t.IsAlive)){
   var troopFaction=swap==0?(troop.OriginalSide==BattleSide.Player?first:second)
                             :(troop.OriginalSide==BattleSide.Player?second:first);
   if(troopFaction==first)firstSurvivors+=troop.RemainingHitPoints;
   else secondSurvivors+=troop.RemainingHitPoints;
  }
 }
 var total=2*samples;
 var entry=new{first,second,budget,samples=total,firstWins,secondWins,draws,
    firstWinRate=(double)firstWins/total,secondWinRate=(double)secondWins/total,
    averageRemainingHpFirst=(double)firstSurvivors/total,averageRemainingHpSecond=(double)secondSurvivors/total};
 results.Add(entry);Console.WriteLine(JsonSerializer.Serialize(entry));
}
var suffix=(disableConvert?"-no-convert":"")+(disableHeal?"-no-heal":"")+
    (floraCostMultiplier==1?"":"-flora-cost-"+floraCostMultiplier.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture));
File.WriteAllText(Path.Combine(root,$"docs/balance-lab-{terrain}{suffix}.json"),
  JsonSerializer.Serialize(new{generatedAt=DateTimeOffset.UtcNow,terrain,disableConvert,disableHeal,floraCostMultiplier,
      scenario="5 stacks each, equal recruitment budget; reversed sides; AI vs AI; 35-round draw limit",
      samplesPerSide=samples,results},new JsonSerializerOptions{WriteIndented=true}));

List<(UnitDefinition Type,int Count)> CreateArmy(string faction,int budget,Random rng){
 var roster=units.Where(unit=>unit.FactionId==faction).Take(5).ToArray();
 if(roster.Length!=5)throw new Exception($"Expected five {faction} troops");
 int Cost(int index)=>roster[index].FactionId=="flora"
     ?(int)Math.Ceiling(roster[index].RecruitmentCost*floraCostMultiplier):roster[index].RecruitmentCost;
 var counts=Enumerable.Repeat(1,5).ToArray();int spent=Enumerable.Range(0,5).Sum(Cost);
 if(spent>budget)throw new Exception($"Budget {budget} cannot field all {faction} types");
 while(true){
  var affordable=Enumerable.Range(0,5).Where(i=>spent+Cost(i)<=budget).ToArray();
  if(affordable.Length==0)break;
  int pick=affordable[rng.Next(affordable.Length)];counts[pick]++;spent+=Cost(pick);
 }
 return Enumerable.Range(0,5).Select(i=>(roster[i],counts[i])).ToList();
}
BattleUnit Create((UnitDefinition Type,int Count) stack,BattleSide side,HexCoord position,string id){
 var unit=stack.Type;
 var ability=disableConvert&&unit.Ability==UnitAbility.ConvertOnHit||
             disableHeal&&unit.Ability==UnitAbility.HealAlly?UnitAbility.None:unit.Ability;
 return new BattleUnit(id,side,position,stack.Count,unit.HitPoints,unit.Damage,
    unit.Speed,unit.Movement,unit.Range,unit.IsRanged,ability:ability,unitTypeId:unit.Id);
}
