using System;
using System.Collections.Generic;
using System.Globalization;

namespace ArcaneForge {
    public sealed class ArtDisplayRule {
        public int artId;
        public bool placeholder;
        public bool playtest;
        public float left,top,width=1f,height=1f;
    }
    public static class ArtDisplayRuleParser {
        public static Dictionary<int,ArtDisplayRule> Parse(IEnumerable<string> lines) {
            var result=new Dictionary<int,ArtDisplayRule>();
            foreach(string line in lines) {
                if(string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))continue;
                string[] fields=line.Split('\t');
                if(fields.Length!=6 && fields.Length!=7)throw new FormatException("Artwork rule must have six or seven fields");
                var rule=new ArtDisplayRule {
                    artId=int.Parse(fields[0],CultureInfo.InvariantCulture),placeholder=fields[1]=="1",
                    playtest=fields.Length==7 && fields[6]=="1",
                    left=float.Parse(fields[2],CultureInfo.InvariantCulture),top=float.Parse(fields[3],CultureInfo.InvariantCulture),
                    width=float.Parse(fields[4],CultureInfo.InvariantCulture),height=float.Parse(fields[5],CultureInfo.InvariantCulture)};
                if(rule.artId<9000000 || rule.artId>10999999 || result.ContainsKey(rule.artId))
                    throw new FormatException("Invalid or duplicate artwork identity");
                if(!rule.placeholder && !(rule.left>=0 && rule.top>=0 && rule.width>0 && rule.height>0
                    && rule.left+rule.width<=1 && rule.top+rule.height<=1))
                    throw new FormatException("Artwork crop is outside the source image");
                result.Add(rule.artId,rule);
            }
            return result;
        }
    }
}
