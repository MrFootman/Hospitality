using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse.AI.Group;
using Verse;
using Verse.AI;

namespace Hospitality
{
    internal static class GuestUtility
    {
        public static DutyDef relaxDef = DefDatabase<DutyDef>.GetNamed("Relax");
        public static DutyDef travelDef = DefDatabase<DutyDef>.GetNamed("Travel");

        private static readonly string labelRecruitSuccess = "LetterLabelMessageRecruitSuccess".Translate(); // from core
        private static readonly string labelRecruitFactionAnger = "LetterLabelRecruitFactionAnger".Translate();
        private static readonly string labelRecruitFactionPlease = "LetterLabelRecruitFactionPlease".Translate();
        private static readonly string labelRecruitFactionChiefAnger = "LetterLabelRecruitFactionChiefAnger".Translate();
        private static readonly string labelRecruitFactionChiefPlease = "LetterLabelRecruitFactionChiefPlease".Translate();
        private static readonly string txtRecruitSuccess = "MessageGuestRecruitSuccess".Translate();
        private static readonly string txtRecruitFactionAnger = "RecruitFactionAnger".Translate();
        private static readonly string txtRecruitFactionPlease = "RecruitFactionPlease".Translate();
        private static readonly string txtRecruitFactionAngerLeaderless = "RecruitFactionAngerLeaderless".Translate();
        private static readonly string txtRecruitFactionPleaseLeaderless = "RecruitFactionPleaseLeaderless".Translate();

        private static readonly StatDef statRecruitRelationshipDamage = StatDef.Named("RecruitRelationshipDamage");
        private static readonly StatDef statPleaseGuestChance = StatDef.Named("PleaseGuestChance");
        private static readonly StatDef statRecruitEffectivity = StatDef.Named("RecruitEffectivity");

        public static bool IsRelaxing(this Pawn pawn)
        {
            return pawn.mindState.duty != null && pawn.mindState.duty.def == relaxDef;
        }

        public static bool IsTraveling(this Pawn pawn)
        {
            return pawn.mindState.duty != null && pawn.mindState.duty.def == travelDef;
        }

        public static bool MayBuy(this Pawn pawn)
        {
            var guestComp = pawn.GetComp<CompGuest>();
            if (guestComp == null) return false;
            return guestComp.mayBuy;
        }

        public static bool IsGuest(this Pawn pawn)
        {
            try
            {
                if (pawn == null) return false;
                if (pawn.Destroyed) return false;
                if (!pawn.Spawned) return false;
                if (pawn.thingIDNumber == 0) return false; // Yeah, this can happen O.O
                if (pawn.Name == null) return false;
                if (pawn.Dead) return false;
                if (pawn.RaceProps == null || !pawn.RaceProps.Humanlike) return false;
                if (pawn.guest == null) return false;
                if (pawn.IsPrisonerOfColony || pawn.Faction == Faction.OfPlayer) return false;
                if (pawn.HostileTo(Faction.OfPlayer)) return false;
                if (!pawn.IsInVisitState()) return false;
                //Log.Message(pawn.NameStringShort+": "+(pawn.mindState.duty!=null?pawn.mindState.duty.def.defName : "null"));
                return true;
            }
            catch(Exception e)
            {
                Log.Warning(e.Message);
                //Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                //Log.Message("Ticks: "+Find.TickManager.TicksGame);
                return false;
            }
        }

        public static bool IsTrader(this Pawn pawn)
        {
            try
            {
                if (pawn == null) return false;
                if (pawn.Destroyed) return false;
                if (!pawn.Spawned) return false;
                if (pawn.thingIDNumber == 0) return false; // Yeah, this can happen O.O
                if (pawn.Name == null) return false;
                if (pawn.Dead) return false;
                if (pawn.RaceProps == null || !pawn.RaceProps.Humanlike) return false;
                if (pawn.guest == null) return false;
                if (pawn.IsPrisonerOfColony || pawn.Faction == Faction.OfPlayer) return false;
                if (pawn.HostileTo(Faction.OfPlayer)) return false;
                if (!pawn.IsInTraderState()) return false;
                return true;
            }
            catch(Exception e)
            {
                Log.Warning(e.Message);
                //Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                //Log.Message("Ticks: "+Find.TickManager.TicksGame);
                return false;
            }
        }

        public static float RecruitPenalty(this Pawn guest)
        {
            return guest.GetStatValue(statRecruitRelationshipDamage);
        }

        public static int GetFriendsInColony(this Pawn guest)
        {
            float requiredOpinion = GetMinRecruitOpinion(guest);
            return guest.MapHeld.mapPawns.FreeColonists.Count(p => RelationsUtility.PawnsKnowEachOther(guest, p) && guest.relations.OpinionOf(p) >= requiredOpinion);
        }

        public static int GetEnemiesInColony(this Pawn guest)
        {
            const int maxOpinion = -20;
            return guest.MapHeld.mapPawns.FreeColonists.Count(p => RelationsUtility.PawnsKnowEachOther(guest, p) && guest.relations.OpinionOf(p) <= maxOpinion);
        }

        public static int GetMinRecruitOpinion(this Pawn guest)
        {
            var difficulty = guest.RecruitDifficulty(Faction.OfPlayer, true);
            var diffSqr = difficulty*difficulty*difficulty*difficulty;
            const int min = 0;
            const int max = 30;
            return Mathf.CeilToInt(Mathf.Lerp(min, max, diffSqr));
        }

        public static bool ImproveRelationship(this Pawn guest)
        {
            var guestComp = guest.GetComp<CompGuest>();
            if (guestComp == null) return false;
            return guestComp.chat;
        }

        public static bool TryRecruit(this Pawn guest)
        {
            var guestComp = guest.GetComp<CompGuest>();
            if (guestComp == null) return false;
            return guestComp.recruit;
        }

        public static bool CanTalkTo(this Pawn talker, Pawn talkee)
        {
            return talker.MapHeld == talkee.MapHeld
                && InteractionUtility.CanInitiateInteraction(talker)
                && InteractionUtility.CanReceiveInteraction(talkee)
                   && (talker.Position - talkee.Position).LengthHorizontalSquared <= 36.0
                   && GenSight.LineOfSight(talker.Position, talkee.Position, talker.MapHeld, true);
        }
        
        public static bool ViableGuestTarget(Pawn guest, bool sleepingIsOk = false)
        {
            return !(!guest.IsGuest() || guest.Downed || (!sleepingIsOk && !guest.Awake()) || !guest.MapHeld.areaManager.Home[guest.Position] || guest.HasDismissiveThought());
        }

        public static void Arrive(this Pawn pawn)
        {
            pawn.PocketHeadgear();

            // Save trader info
            bool trader = pawn.mindState.wantsToTradeWithColony;
            TraderKindDef traderKindDef = trader?pawn.trader.traderKind:null;

            pawn.guest.SetGuestStatus(Faction.OfPlayer);

            // Restore trader info
            if (trader)
            {
                pawn.mindState.wantsToTradeWithColony = trader;
                PawnComponentsUtility.AddAndRemoveDynamicComponents(pawn);
                pawn.trader.traderKind = traderKindDef;
            }

            pawn.GetComp<CompGuest>().Arrive();
        }

        public static void Leave(this Pawn pawn)
        {
            pawn.WearHeadgear();

            pawn.needs.AddOrRemoveNeedsAsAppropriate();

            pawn.guest.SetGuestStatus(null);

            pawn.GetComp<CompGuest>().Leave();

            //var reservationManager = pawn.MapHeld.reservationManager;
            //var allReservedThings = reservationManager.AllReservedThings().ToArray();
            //foreach (var t in allReservedThings)
            //{
            //    if (reservationManager.ReservedBy(t, pawn)) reservationManager.Release(t, pawn);
            //}
        }

        private static bool IsInVisitState(this Pawn guest)
        {
            var lord = guest.GetLord();
            if (lord == null) return false;

            var job = lord.LordJob;
            return  job is LordJob_VisitColony;
        }

        private static bool IsInTraderState(this Pawn guest)
        {
            var lord = guest.GetLord();
            if (lord == null) return false;

            var job = lord.LordJob;
            return  job is LordJob_TradeWithColony;
        }

        public static bool HasDismissiveThought(this Pawn guest)
        {
            return guest.needs.mood.thoughts.memories.Memories.Any(t => t.def.defName == "GuestDismissiveAttitude");
        }

        public static Pawn[] GetAllGuests(Map map)
        {
            return map.mapPawns.AllPawnsSpawned.Where(IsGuest).ToArray();
        }

        public static void AddNeedJoy(Pawn pawn)
        {
            if (pawn.needs.joy == null)
            {
                var addNeed = typeof (Pawn_NeedsTracker).GetMethod("AddNeed", BindingFlags.Instance | BindingFlags.NonPublic);
                addNeed.Invoke(pawn.needs, new object[] { DefDatabase<NeedDef>.GetNamed("Joy") });
            }
            pawn.needs.joy.CurLevel = Rand.Range(0, 0.5f);
        }

        public static void AddNeedComfort(Pawn pawn)
        {
            if (pawn.needs.comfort == null)
            {
                var addNeed = typeof (Pawn_NeedsTracker).GetMethod("AddNeed", BindingFlags.Instance | BindingFlags.NonPublic);
                addNeed.Invoke(pawn.needs, new object[] { DefDatabase<NeedDef>.GetNamed("Comfort") });
            }
            pawn.needs.comfort.CurLevel = Rand.Range(0, 0.5f);
        }

        public static Building_GuestBed FindBedFor(this Pawn pawn)
        {
            Predicate<Thing> bedValidator = delegate(Thing t) {
                                                if (!(t is Building_GuestBed)) return false;
                                                if (!pawn.CanReserveAndReach(t, PathEndMode.OnCell, Danger.Some)) return false;
                                                var b = (Building_GuestBed) t;
                                                if (b.CurOccupant != null) return false;
                                                if (b.ForPrisoners) return false;
                                                Find.Maps.ForEach(m => m.reservationManager.ReleaseAllForTarget(b)); // TODO: Put this somewhere smarter
                                                return (!b.IsForbidden(pawn) && !b.IsBurning());
                                            };
            var bed = (Building_GuestBed)GenClosest.ClosestThingReachable(pawn.GetLord().CurLordToil.FlagLoc, pawn.MapHeld, ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial), PathEndMode.OnCell, TraverseParms.For(pawn), 500f, bedValidator);
            return bed;
        }

        public static void PocketHeadgear(this Pawn pawn)
        {
            var headgear = pawn.apparel.WornApparel.Where(CoversHead).ToArray();
            foreach (var apparel in headgear)
            {
                if (pawn.GetInventorySpaceFor(apparel) < 1) continue;
                
                Apparel droppedApp;
                if (pawn.apparel.TryDrop(apparel, out droppedApp))
                {
                    bool success = pawn.inventory.innerContainer.TryAdd(droppedApp);
                    if(!success) pawn.apparel.Wear(droppedApp);
                }
            }
        }

        private static bool CoversHead(this Apparel a)
        {
            return a.def.apparel.bodyPartGroups.Any(
                g =>
                    g == BodyPartGroupDefOf.Eyes || g == BodyPartGroupDefOf.UpperHead
                    || g == BodyPartGroupDefOf.FullHead);
        }

        public static void WearHeadgear(this Pawn pawn)
        {
            var container = pawn.inventory.innerContainer;
            var headgear = container.OfType<Apparel>().Where(CoversHead).InRandomOrder().ToArray();
            foreach (var apparel in headgear)
            {
                if (pawn.apparel.CanWearWithoutDroppingAnything(apparel.def))
                {
                    pawn.apparel.Wear(apparel);
                    container.Remove(apparel);
                }
            }
        }

        public static void FixTimetable(this Pawn pawn)
        {
            if (pawn.mindState == null) pawn.mindState = new Pawn_MindState(pawn);
            pawn.timetable = new Pawn_TimetableTracker(pawn) {times = new List<TimeAssignmentDef>(24)};
            for (int i = 0; i < 24; i++)
            {
                var def = TimeAssignmentDefOf.Anything;
                pawn.timetable.times.Add(def);
            }
        }

        public static void FixDrugPolicy(this Pawn pawn)
        {
            //if (pawn.drugs == null) 
            pawn.drugs = new Pawn_DrugPolicyTracker(pawn)
            {
                CurrentPolicy = pawn.GetComp<CompGuest>().GetDrugPolicy(pawn)
            };
        }

        public static void CheckRecruitingSuccessful(this Pawn guest, Pawn recruiter, List<RulePackDef> extraSentencePacks)
        {
            if (!guest.TryRecruit()) return;

            var friends = guest.GetFriendsInColony();
            var friendsRequired = FriendsRequired(guest.MapHeld) + guest.GetEnemiesInColony();
            float friendPercentage = 100f * friends / friendsRequired;

            //Log.Message(String.Format("Recruiting {0}: diff: {1} mood: {2}", guest.NameStringShort,recruitDifficulty, colonyTrust));
            if (friendPercentage > 99)
            {
                RecruitingSuccess(guest);
            }
            else
            {
                TryPleaseGuest(recruiter, guest, true, extraSentencePacks);
            }
        }

        private static void RecruitingSuccess(Pawn guest)
        {
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDef.Named("RecruitGuest"), KnowledgeAmount.Total);

            Find.LetterStack.ReceiveLetter(labelRecruitSuccess, String.Format(txtRecruitSuccess, guest), LetterDefOf.Good, guest);

            if (guest.Faction != Faction.OfPlayer)
            {
                if (guest.Faction != null)
                {
                    guest.Faction.AffectGoodwillWith(Faction.OfPlayer, -guest.RecruitPenalty());
                    if (guest.RecruitPenalty() >= 1)
                    {
                        //Log.Message("txtRecruitFactionAnger");
                        string message;
                        if (guest.Faction.leader != null)
                        {
                            message = String.Format(txtRecruitFactionAnger, guest.Faction.leader.Name, guest.Faction.Name, guest.NameStringShort, (-guest.RecruitPenalty()).ToStringByStyle(ToStringStyle.Integer, ToStringNumberSense.Offset));
                            Find.LetterStack.ReceiveLetter(labelRecruitFactionChiefAnger, message, LetterDefOf.BadNonUrgent);
                        }
                        else
                        {
                            message = String.Format(txtRecruitFactionAngerLeaderless, guest.Faction.Name, guest.NameStringShort, (-guest.RecruitPenalty()).ToStringByStyle(ToStringStyle.Integer, ToStringNumberSense.Offset));
                            Find.LetterStack.ReceiveLetter(labelRecruitFactionAnger, message, LetterDefOf.BadNonUrgent);
                        }
                    }
                    else if (guest.RecruitPenalty() <= -1)
                    {
                        //Log.Message("txtRecruitFactionPlease");
                        string message;
                        if (guest.Faction.leader != null)
                        {
                            message = String.Format(txtRecruitFactionPlease, guest.Faction.leader.Name, guest.Faction.Name, guest.NameStringShort, (-guest.RecruitPenalty()).ToStringByStyle(ToStringStyle.Integer, ToStringNumberSense.Offset));
                            Find.LetterStack.ReceiveLetter(labelRecruitFactionChiefPlease, message, LetterDefOf.Good);
                        }
                        else
                        {
                            message = String.Format(txtRecruitFactionPleaseLeaderless, guest.Faction.Name, guest.NameStringShort, (-guest.RecruitPenalty()).ToStringByStyle(ToStringStyle.Integer, ToStringNumberSense.Offset));
                            Find.LetterStack.ReceiveLetter(labelRecruitFactionPlease, message, LetterDefOf.Good);
                        }
                    }
                }
                guest.Adopt();
            }
            var taleParams = new object[] {guest.MapHeld.mapPawns.FreeColonistsSpawned.RandomElement(), guest};
            TaleRecorder.RecordTale(TaleDef.Named("Recruited"), taleParams);
        }

        public static void Adopt(this Pawn guest)
        {
            // Clear mind
            guest.pather.StopDead();
            if (guest.jobs.jobQueue != null) guest.jobs.jobQueue.Clear();
            guest.jobs.EndCurrentJob(JobCondition.InterruptForced);

            guest.inventory.innerContainer.TryDropAll(guest.Position, guest.MapHeld, ThingPlaceMode.Near);

            // Clear reservations
            Find.Maps.ForEach(m => m.reservationManager.ReleaseAllClaimedBy(guest));

            guest.SetFaction(Faction.OfPlayer);
            // TODO: Check that this works with multiple bases

            guest.mindState.exitMapAfterTick = -99999;
            guest.MapHeld.mapPawns.UpdateRegistryForPawn(guest);

            guest.playerSettings.medCare = MedicalCareCategory.Best;
            guest.playerSettings.AreaRestriction = null;

            if (guest.caller != null) guest.caller.DoCall();
        }

        public static float AdjustPleaseChance(float pleaseChance, Pawn recruiter, Pawn target)
        {
            var opinion = target.relations.OpinionOf(recruiter);
            //Log.Message(String.Format("Opinion of {0} about {1}: {2}", target.NameStringShort,recruiter.NameStringShort, opinion));
            //Log.Message(String.Format("{0} + {1} = {2}", pleaseChance, opinion*0.01f, pleaseChance + opinion*0.01f));
            return pleaseChance + opinion*0.01f;
        }

        public static void GainSocialThought(Pawn initiator, Pawn target, ThoughtDef thoughtDef)
        {
            if (!ThoughtUtility.CanGetThought(target, thoughtDef)) return;

            float impact = initiator.GetStatValue(StatDefOf.SocialImpact);
            var thoughtMemory = (Thought_Memory) ThoughtMaker.MakeThought(thoughtDef);
            thoughtMemory.moodPowerFactor = impact;
            
            var thoughtSocialMemory = thoughtMemory as Thought_MemorySocial;
            if (thoughtSocialMemory != null)
            {
                thoughtSocialMemory.opinionOffset *= impact;
            }
            target.needs.mood.thoughts.memories.TryGainMemory(thoughtMemory, initiator);
        }

        public static bool ShouldRecruit(this Pawn pawn, Pawn guest)
        {
            if (!ViableGuestTarget(guest, true)) return false;
            if (!guest.TryRecruit()) return false;
            if (guest.InMentalState) return false;
            //if (guest.relations.OpinionOf(pawn) >= 100) return false;
            //if (guest.RelativeTrust() < 50) return false;
            if (guest.relations.OpinionOf(pawn) <= -10) return false;
            //if (guest.interactions.InteractedTooRecentlyToInteract()) return false;
            //if (pawn.interactions.InteractedTooRecentlyToInteract()) return false;
            if (!guest.Awake()) return false;
            if (!pawn.CanReserveAndReach(guest, PathEndMode.OnCell, pawn.NormalMaxDanger())) return false;

            return true;
        }

        public static bool ShouldImproveRelationship(this Pawn pawn, Pawn guest)
        {
            if (!ViableGuestTarget(guest)) return false;
            if (!guest.ImproveRelationship()) return false;
            //if (guest.Faction.ColonyGoodwill >= 100) return false;
            if (guest.relations.OpinionOf(pawn) >= 100) return false;
            if (guest.InMentalState) return false;
            //if (guest.interactions.InteractedTooRecentlyToInteract()) return false;
            //if (pawn.interactions.InteractedTooRecentlyToInteract()) return false;
            if (!pawn.CanReserveAndReach(guest, PathEndMode.OnCell, pawn.NormalMaxDanger())) return false;

            return true;
        }

        public static void TryGiveBackpack(this Pawn p)
        {
            var def = DefDatabase<ThingDef>.GetNamed("Apparel_Backpack", false);
            if (def == null) return;

            if (p.inventory.innerContainer.Contains(def)) return;

            ThingDef stuff = GenStuff.RandomStuffFor(def);
            var item = (Apparel)ThingMaker.MakeThing(def, stuff);
            item.stackCount = 1;
            p.apparel.Wear(item, false);
        }

        public static int GetInventorySpaceFor(this Pawn pawn, Thing current)
        {
            // Combat Realism
            var inventory = pawn.GetInventory();
            if (inventory == null) return current.stackCount;

            object[] parameters = {current, 0, false, false};
            var success = (bool)inventory
                .GetType()
                .GetMethod("CanFitInInventory", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(inventory, parameters);
            if (!success) return 0;
            var count = (int) parameters[1];

            return count;
        }

        private static ThingComp GetInventory(this Pawn pawn)
        {
            return pawn.AllComps.FirstOrDefault(c => c.GetType().Name == "CompInventory");
        }

        public static void Break(this Pawn pawn)
        {
            if (!pawn.Spawned || pawn.Dead || pawn.Downed || pawn.InMentalState) return;

            pawn.guest.SetGuestStatus(null);
            bool canFlee = pawn.Map.reachability.CanReachMapEdge(pawn.PositionHeld, TraverseParms.For(TraverseMode.NoPassClosedDoors));
            
            var mentalState = canFlee ? MentalStateDefOf.PanicFlee : MentalStateDefOf.ManhunterPermanent;

            pawn.mindState.mentalStateHandler.TryStartMentalState(mentalState);
        }

        public static void ShowRescuedPawnDialog(Pawn pawn)
        {
            if (pawn.story.traits == null) throw new Exception(pawn.Name + "'s traits are null!");
            
            string textAsk = "RescuedInitial".Translate(pawn.GetTitle().ToLower(), GenText.ToCommaList(pawn.story.traits.allTraits.Select(t=>t.Label)));
            textAsk = textAsk.AdjustedFor(pawn);
            PawnRelationUtility.TryAppendRelationsWithColonistsInfo(ref textAsk, pawn);
            DiaNode nodeAsk = new DiaNode(textAsk);
            var textAccept = "RescuedInitial_Accept".Translate();
            textAccept = textAccept.AdjustedFor(pawn);

            DiaOption optionAccept = new DiaOption(textAccept)
            {
                action = () => OptionAdopt(pawn), 
                resolveTree = true
            };
            nodeAsk.options.Add(optionAccept);

            var textReject = "RescuedInitial_Reject".Translate();
            textReject = textReject.AdjustedFor(pawn);

            DiaOption optionReject = new DiaOption(textReject)
            {
                action = null, 
                resolveTree = true
            };

            nodeAsk.options.Add(optionReject);
            Find.WindowStack.Add(new Dialog_NodeTree(nodeAsk, true));
        }

        public static string GetTitle(this Pawn pawn)
        {
            var title = pawn.story.adulthood != null ? pawn.story.adulthood.Title : pawn.story.childhood != null ? pawn.story.childhood.Title : pawn.KindLabel;
            return title;
        }

        private static void OptionAdopt(Pawn pawn)
        {
            pawn.Adopt();
            CameraJumper.TryJump(pawn);
            Find.LetterStack.ReceiveLetter(labelRecruitSuccess, String.Format(txtRecruitSuccess, pawn), LetterDefOf.Good, pawn);
        }

        public static void BreakupRelations(Pawn pawn)
        {
            var relations = pawn.relations.DirectRelations.Where(r => !r.otherPawn.Dead && r.otherPawn.Faction != null && r.otherPawn.Faction.IsPlayer && LovePartnerRelationUtility.LovePartnerRelationExists(pawn, r.otherPawn)).ToArray();
            var breakup = new InteractionWorker_Breakup();
            foreach (var relation in relations)
            {
                breakup.Interacted(relation.otherPawn, pawn, null);
            }
            Faction hostileFaction;
            if (
                Find.FactionManager.AllFactions.Where(f => f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer))
                    .TryRandomElement(out hostileFaction))
            {
                pawn.SetFaction(hostileFaction);
            }
        }

        public static Area GetGuestArea(this Pawn p)
        {
            var compGuest = p.GetComp<CompGuest>();
            if (compGuest == null) return null;

            return compGuest.GuestArea;
        }

        public static bool Bought(this Pawn pawn, Thing thing)
        {
            var comp = pawn.GetComp<CompGuest>();
            if (comp == null) return false;

            //Log.Message(pawn.NameStringShort+": bought "+thing.Label + "? " + (comp.boughtItems.Contains(thing.thingIDNumber) ? "Yes" : "No"));
            return comp.boughtItems.Contains(thing.thingIDNumber);
        }

        public static bool WillRescueJoin(Pawn pawn)
        {
            if (DebugSettings.instantRecruit) return true;
            if (IsEnvironmentHostile(pawn)) return true;

            float chance = 1 - pawn.RecruitDifficulty(Faction.OfPlayer, false)*0.5f; // was 0.75f
            chance = Mathf.Clamp(chance, 0.005f, 1f);

            Rand.PushState();
            Rand.Seed = pawn.HashOffset();
            float value = Rand.Value;
            Rand.PopState();

            return value <= chance;
        }

        private static bool IsEnvironmentHostile(Pawn pawn)
        {
            return !pawn.SafeTemperatureRange().Includes(pawn.Map.mapTemperature.OutdoorTemp) || pawn.Map.gameConditionManager.ConditionIsActive(GameConditionDefOf.ToxicFallout);
        }

        public static void PlanNewVisit(IIncidentTarget map, float afterDays, Faction faction = null)
        {
            IncidentParms incidentParms = StorytellerUtility.DefaultParmsNow(Find.Storyteller.def, IncidentCategory.AllyArrival, map);

            if(faction != null) incidentParms.faction = faction;
            var incident = new FiringIncident(IncidentDefOf.VisitorGroup, null, incidentParms);
            var qi = new QueuedIncident(incident, (int) (Find.TickManager.TicksGame + GenDate.TicksPerDay*afterDays));
            Find.Storyteller.incidentQueue.Add(qi);
        }

        public static bool IsInGuestZone(this Pawn p, Thing s)
        {
            var area = p.GetGuestArea();
            if (area == null) return true;
            return area[s.Position];
        }

        public static IEnumerable<Building_GuestBed> GetGuestBeds(this Pawn pawn)
        {
            var area = pawn.GetGuestArea();
            if (area == null) return pawn.MapHeld.listerBuildings.AllBuildingsColonistOfClass<Building_GuestBed>();
            return pawn.MapHeld.listerBuildings.AllBuildingsColonistOfClass<Building_GuestBed>().Where(b => area[b.Position]);
        }

        public static int FriendsRequired(Map map)
        {
            var required = map.mapPawns.FreeColonistsCount /3.75f;
            if (required < 1) return 1;
            else return Mathf.RoundToInt(required);
        }

        public static Pawn EndorseColonists(Pawn recruiter, Pawn guest)
        {
            if (guest.relations == null) return null;
            if (recruiter.relations == null) return null;

            Pawn target;
            var pawns = guest.MapHeld.mapPawns.FreeColonistsSpawned.Where(c=> c != recruiter && recruiter.relations.OpinionOf(c) > 0).ToArray();
            if (pawns.Length == 0) return null;

            if (pawns.TryRandomElement(out target))
            {
                GainSocialThought(target, guest, ThoughtDef.Named("EndorsedByRecruiter"));

                //Log.Message(recruiter.NameStringShort + " endorsed " + target + " to " + guest.Name);
            }
            return target;
        }

        public static void TryPleaseGuest(Pawn recruiter, Pawn guest, bool focusOnRecruiting, List<RulePackDef> extraSentencePacks)
        {
            // TODO: pawn.records.Increment(RecordDefOf.GuestsCharmAttempts);
            recruiter.skills.Learn(SkillDefOf.Social, 35f);
            float pleaseChance = recruiter.GetStatValue(statPleaseGuestChance);
            pleaseChance = AdjustPleaseChance(pleaseChance, recruiter, guest);
            pleaseChance = Mathf.Clamp01(pleaseChance);

            var failedCharms = guest.GetComp<CompGuest>().failedCharms;

            if (Rand.Value > pleaseChance)
            {
                var isAbrasive = recruiter.story.traits.HasTrait(TraitDefOf.Abrasive);
                int multiplier = isAbrasive ? 2 : 1;
                string multiplierText = multiplier > 1 ? " x" + multiplier : String.Empty;

                int amount;
                if (failedCharms.TryGetValue(recruiter, out amount))
                {
                    amount++;
                    failedCharms[recruiter] = amount;
                }
                else
                {
                    failedCharms.Add(recruiter, 1);
                }

                if (amount >= 3)
                {
                    Messages.Message(
                        "RecruitAngerMultiple".Translate(recruiter.NameStringShort, guest.NameStringShort, amount),
                        guest, MessageSound.Negative);
                }

                extraSentencePacks.Add(RulePackDef.Named("Sentence_CharmAttemptRejected"));
                for (int i = 0; i < multiplier; i++)
                {
                    GainSocialThought(recruiter, guest, ThoughtDef.Named("GuestOffendedRelationship"));
                }

                MoteMaker.ThrowText((recruiter.DrawPos + guest.DrawPos) / 2f, recruiter.Map, "TextMote_CharmFail".Translate()+multiplierText, 8f);
            }
            else
            {
                
                failedCharms.Remove(recruiter);

                var statValue = recruiter.GetStatValue(statRecruitEffectivity);
                var floor = Mathf.FloorToInt(statValue);
                int multiplier = floor + (Rand.Value < statValue - floor ? 1 : 0);

                // Multiplier is for what the focus is one
                for (int i = 0; i < multiplier; i++)
                {
                    if(focusOnRecruiting)
                        EndorseColonists(recruiter, guest);
                    else
                        GainSocialThought(recruiter, guest, ThoughtDef.Named("GuestPleasedRelationship"));
                }
                
                // And then one more of the other
                multiplier++; 
                if (focusOnRecruiting)
                    GainSocialThought(recruiter, guest, ThoughtDef.Named("GuestPleasedRelationship"));
                else
                    EndorseColonists(recruiter, guest);

                extraSentencePacks.Add(RulePackDef.Named("Sentence_CharmAttemptAccepted"));

                string multiplierText = multiplier > 1 ? " x" + multiplier : String.Empty;
                MoteMaker.ThrowText((recruiter.DrawPos + guest.DrawPos) / 2f, recruiter.Map, "TextMote_CharmSuccess".Translate() + multiplierText, 8f);
            }
            GainSocialThought(recruiter, guest, ThoughtDef.Named("GuestDismissiveAttitude"));
        }

        public const int InteractIntervalAbsoluteMin = 360; // changed from 120
    }
}