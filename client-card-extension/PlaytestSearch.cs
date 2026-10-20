using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ArcaneForge {
    public static class PlaytestSearch {
        const string Tag="ARCANEPLAYTESTTAG";
        static HashSet<uint> playtestArt;
        public static bool ApplyTitleFilter(object filter,object group) {
            var filterType=filter.GetType();
            var value=filterType.GetField("Value").GetValue(filter);
            // UnlocalizedMTGAString.ToString returns its search key. It has no
            // RawText property (that belongs to other native text wrappers).
            string text=value?.ToString();
            bool playtest=string.Equals(text,Tag,StringComparison.OrdinalIgnoreCase);
            bool added=string.Equals(text,"ARCANEADDEDTAG",StringComparison.OrdinalIgnoreCase);
            bool token=string.Equals(text,"ARCANETOKENTAG",StringComparison.OrdinalIgnoreCase);
            if(!playtest && !added && !token)return false;
            if(playtestArt==null) {
                var rules=ArtDisplayRuleParser.Parse(File.ReadAllLines(Path.Combine(Application.streamingAssetsPath,"ArcaneForgeCards","art-display.tsv")));
                playtestArt=new HashSet<uint>(rules.Values.Where(r=>r.playtest).Select(r=>(uint)r.artId));
            }
            bool negate=(bool)filterType.GetField("Negate").GetValue(filter);
            var cards=(IEnumerable)group.GetType().GetField("Cards").GetValue(group);
            System.Reflection.FieldInfo cardField=null;
            System.Reflection.PropertyInfo artProperty=null;
            System.Reflection.MethodInfo fail=null;
            object reason=null;
            foreach(var item in cards) {
                if(cardField==null) {
                    cardField=item.GetType().GetField("Card");
                    fail=item.GetType().GetMethod("FailFilter");
                    reason=Enum.ToObject(fail.GetParameters()[0].ParameterType,0x800);
                }
                var card=cardField.GetValue(item);
                if(artProperty==null)artProperty=card.GetType().GetProperty("ArtId");
                uint art=(uint)artProperty.GetValue(card);
                bool matches=token?(bool)card.GetType().GetProperty("IsToken").GetValue(card):
                    added?art>=9000000 && art<10910000:playtestArt.Contains(art);
                if(matches==negate)fail.Invoke(item,new[]{reason});
            }
            return true;
        }
        // Expand our local tag into Arena's existing title-filter syntax.
        // Quoted text remains literal, so searching a card name is unchanged.
        public static string Rewrite(string input) {
            if(string.IsNullOrEmpty(input))return input;
            const string pattern="\"[^\"]*\"|(?<![\\w:])-?(?:tag|is):(?:playtest|added|custom|token|planeswalker|creature|land|artifact|enchantment|instant|sorcery|battle)\\b";
            if(!Regex.IsMatch(input,@"\b(?:tag|is):",RegexOptions.IgnoreCase))return input;
            try {
                return Regex.Replace(input,pattern,m=> {
                    if(m.Value.StartsWith("\"",StringComparison.Ordinal))return m.Value;
                    string prefix=m.Value.StartsWith("-",StringComparison.Ordinal)?"-":"";
                    string tag=m.Value.Substring(m.Value.IndexOf(':')+1).ToLowerInvariant();
                    return prefix+(tag=="playtest"?"name:"+Tag:tag=="added" || tag=="custom"?"name:ARCANEADDEDTAG":
                        tag=="token"?"name:ARCANETOKENTAG":"t:"+tag);
                },RegexOptions.IgnoreCase);
            } catch(Exception error) {Debug.LogWarning("ArcaneForge playtest search: "+error.Message);return input;}
        }
    }
}
