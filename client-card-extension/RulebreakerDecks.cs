using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ArcaneForge {
    public static class RulebreakerDecks {
        sealed class Rule {public bool unlimited; public int budget; public HashSet<uint> exempt,extra;}
        static Dictionary<uint,Rule> titles,printings;
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static object Get(object obj,string name) {
            if(obj==null)return null;
            var type=obj.GetType();
            var property=type.GetProperty(name,Flags);
            return property!=null?property.GetValue(obj):type.GetField(name,Flags)?.GetValue(obj);
        }
        static uint Number(object obj)=>obj==null?0:Convert.ToUInt32(obj);
        static IEnumerable<object> Items(object value)=>(value as IEnumerable)?.Cast<object>()??Enumerable.Empty<object>();
        static HashSet<uint> Ids(string text)=>new HashSet<uint>(text.Split(',').Where(s=>s.Length>0).Select(uint.Parse));
        static void Load() {
            if(titles!=null)return;
            titles=new Dictionary<uint,Rule>();printings=new Dictionary<uint,Rule>();
            string path=Path.Combine(Application.streamingAssetsPath,"ArcaneForgeCards","rulebreaker-decks.tsv");
            if(!File.Exists(path))return;
            foreach(string line in File.ReadAllLines(path)) {
                var fields=line.Split('\t');if(fields.Length!=6)continue;
                var rule=new Rule {unlimited=fields[2]=="1",budget=int.Parse(fields[3]),exempt=Ids(fields[4]),extra=Ids(fields[5])};
                titles[uint.Parse(fields[0])]=rule;printings[uint.Parse(fields[1])]=rule;
            }
        }
        static List<Rule> Rules(IEnumerable<object> commanders) {
            Load();var result=new List<Rule>();
            foreach(var commander in commanders)if(titles.TryGetValue(Number(Get(commander,"TitleId")),out var rule))result.Add(rule);
            return result;
        }
        static List<Rule> ContextRules(object context) {
            var result=Rules(Items(Get(Get(context,"ReadOnlyCommanderInfo"),"Commanders")));
            if(result.Count==0)foreach(var card in Items(Get(Get(context,"Deck"),"CommandZone")))
                if(printings.TryGetValue(Number(Get(card,"cardId")),out var rule))result.Add(rule);
            return result;
        }
        static bool Exempt(uint title,List<Rule> rules)=>rules.Any(r=>r.exempt.Contains(title));
        static HashSet<uint> Colors(object card)=>new HashSet<uint>(Items(Get(card,"ColorIdentity")).Select(Number));
        static object Model(object value,int depth=0) {
            if(value==null || depth>3)return null;
            if(value.GetType().GetMethod("GetFilteredCommandZone",Flags)!=null)return value;
            foreach(var field in value.GetType().GetFields(Flags)) {
                if(field.Name=="model" || field.FieldType.Name.Contains("DisplayClass")) {
                    var found=Model(field.GetValue(value),depth+1);if(found!=null)return found;
                }
            }
            return null;
        }
        // Called only by the deck builder's commander-color predicate.
        public static bool CardMatches(object card,uint colors,object model) {
            uint identity=Number(Get(card,"ColorIdentityFlags"));
            if((identity & ~colors)==0 || colors==0 && (bool)(Get(card,"IsBasicLand")??false))return true;
            model=Model(model);if(model==null)return false;
            var commanders=Items(model.GetType().GetMethod("GetFilteredCommandZone",Flags).Invoke(model,null)).Select(c=>Get(c,"Printing"));
            var rules=Rules(commanders);uint title=Number(Get(card,"TitleId"));
            if(Exempt(title,rules))return true;
            uint missing=identity & ~colors;int count=0;while(missing!=0){count+=(int)(missing&1);missing>>=1;}
            return rules.Any(r=>r.extra.Contains(title) && count<=r.budget);
        }
        // Keep the native identity check for every card outside an active exception.
        public static bool ExemptsTitle(object context,object key) {
            var rules=ContextRules(context);if(rules.Count==0)return false;
            uint title=Number(Get(key,"TitleId"));if(Exempt(title,rules))return true;
            if(!rules.Any(r=>r.extra.Contains(title)))return false;
            var identity=new HashSet<uint>(Items(Get(Get(context,"ReadOnlyCommanderInfo"),"Commanders")).SelectMany(Colors));
            var chosen=new HashSet<uint>();
            foreach(DictionaryEntry entry in (IDictionary)Get(context,"CardQuantitiesByTitleId")) {
                var candidate=Get(entry.Value,"CardInfo");uint candidateTitle=Number(Get(candidate,"TitleId"));
                if(Exempt(candidateTitle,rules) || !rules.Any(r=>r.extra.Contains(candidateTitle)))continue;
                chosen.UnionWith(Colors(candidate).Where(c=>!identity.Contains(c)));
            }
            return chosen.Count<=rules.Max(r=>r.budget);
        }
        public static bool AllowsMainSize(object context,object key) {
            if(Get(key,"Pile")?.ToString()!="MainDeck" || !ContextRules(context).Any(r=>r.unlimited))return false;
            var pile=context.GetType().GetMethod("PileContext",Flags).Invoke(context,new[]{key});
            int commanders=Items(Get(Get(context,"Deck"),"CommandZone")).Count();
            return Number(Get(pile,"CardsInPileQuantity"))>=100-commanders;
        }
        public static int SanityMaximum(int original,object context)=>ContextRules(context).Any(r=>r.unlimited)?int.MaxValue/4:original;
        public static void AdjustValidationResult(object result,object context) {
            if(!ContextRules(context).Any(r=>r.unlimited))return;
            foreach(string name in new[]{"MaxTotalCards","MaxMainDeckCards"}) {
                var property=result.GetType().GetProperty(name,Flags);
                if(property?.CanWrite==true)property.SetValue(result,Convert.ChangeType(10000,property.PropertyType));
                else {
                    var field=result.GetType().GetField(name,Flags);
                    if(field!=null)field.SetValue(result,Convert.ChangeType(10000,field.FieldType));
                }
            }
        }
    }
}
