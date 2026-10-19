using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ArcaneForge {
    public static class LocalCardArt {
        static readonly Dictionary<int,Texture2D> Cache=new Dictionary<int,Texture2D>();
        static Dictionary<int,ArtDisplayRule> DisplayRules;
        static ArtDisplayRule Rule(int id) {
            if(DisplayRules==null) {
                DisplayRules=new Dictionary<int,ArtDisplayRule>();
                string path=Path.Combine(Application.streamingAssetsPath,"ArcaneForgeCards","art-display.tsv");
                if(File.Exists(path)) {
                    try {
                        DisplayRules=ArtDisplayRuleParser.Parse(File.ReadAllLines(path));
                        Debug.Log("ArcaneForge artwork display rules loaded: "+DisplayRules.Count);
                    } catch(Exception error) { Debug.LogWarning("ArcaneForge artwork display rules: "+error.Message); }
                } else Debug.LogWarning("ArcaneForge artwork display rules file is missing");
            }
            DisplayRules.TryGetValue(id,out ArtDisplayRule result);
            return result;
        }
        static Texture2D Placeholder() {
            // A neutral UI backdrop for playtest cards that have no illustration.
            // Do not display printed card rules again inside the art window.
            var texture=new Texture2D(512,360,TextureFormat.RGBA32,false);
            var pixels=new Color[512*360];
            for(int y=0;y<360;y++)for(int x=0;x<512;x++)
                pixels[y*512+x]=Color.Lerp(new Color(.10f,.13f,.16f),new Color(.22f,.26f,.29f),(x+y)/870f);
            texture.SetPixels(pixels);
            return texture;
        }
        static void MarkPlaytest(Texture2D texture) {
            // A small high-contrast UI badge; no font assets or scene objects needed.
            string[] letters={"111101101111100100100", "100100100100100100111",
                "010101101111101101101", "101101101010010010010",
                "111010010010010010010", "111100100110100100111",
                "111100100111001001111", "111010010010010010010"};
            int scale=Math.Max(1,texture.width/180),pad=3*scale;
            int w=33*scale+pad*2,h=7*scale+pad*2;
            int x0=pad,y0=texture.height-h-pad;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                texture.SetPixel(x0+x,y0+y,new Color(.04f,.06f,.08f,1f));
            for(int i=0;i<letters.Length;i++)for(int row=0;row<7;row++)for(int col=0;col<3;col++)
                if(letters[i][row*3+col]=='1')for(int dy=0;dy<scale;dy++)for(int dx=0;dx<scale;dx++)
                    texture.SetPixel(x0+pad+(i*4+col)*scale+dx,y0+pad+(6-row)*scale+dy,new Color(1f,.90f,.58f,1f));
        }
        public static bool Has(string assetPath) {
            if(string.IsNullOrEmpty(assetPath))return false;
            string filename=Path.GetFileNameWithoutExtension(assetPath);
            int separator=filename.IndexOf('_');
            return separator>=0 && int.TryParse(filename.Substring(0,separator),out int id)
                && id>0
                && File.Exists(Path.Combine(Application.streamingAssetsPath,"ArcaneForgeCards",id+".jpg"));
        }
        public static Texture2D Load(string assetPath) {
            if(string.IsNullOrEmpty(assetPath))return null;
            string filename=Path.GetFileNameWithoutExtension(assetPath);
            int separator=filename.IndexOf('_');
            if(separator<0 || !int.TryParse(filename.Substring(0,separator),out int id)
                || id<=0)return null;
            if(Cache.TryGetValue(id,out Texture2D cached))return cached;
            string path=Path.Combine(Application.streamingAssetsPath,"ArcaneForgeCards",id+".jpg");
            if(!File.Exists(path))return null;
            var rule=Rule(id);
            if(rule!=null && rule.placeholder) {
                Debug.Log("ArcaneForge placeholder artwork: "+id);
                var fallback=Placeholder();fallback.name="ArcaneForge no illustration "+id;
                if(rule.playtest)MarkPlaytest(fallback);
                fallback.Apply(false,true);
                fallback.wrapMode=TextureWrapMode.Clamp;Cache[id]=fallback;return fallback;
            }
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            bool crop=rule!=null && rule.width>0f && rule.height>0f
                && (rule.left>0f || rule.top>0f || rule.width<1f || rule.height<1f);
            if(!ImageConversion.LoadImage(texture,File.ReadAllBytes(path),!(crop || rule!=null && rule.playtest))) {
                UnityEngine.Object.Destroy(texture);return null;
            }
            if(crop) {
                Debug.Log("ArcaneForge cropped artwork: "+id);
                int x=Mathf.Clamp(Mathf.RoundToInt(rule.left*texture.width),0,texture.width-1);
                int y=Mathf.Clamp(Mathf.RoundToInt((1f-rule.top-rule.height)*texture.height),0,texture.height-1);
                int width=Mathf.Clamp(Mathf.RoundToInt(rule.width*texture.width),1,texture.width-x);
                int height=Mathf.Clamp(Mathf.RoundToInt(rule.height*texture.height),1,texture.height-y);
                var cropped=new Texture2D(width,height,TextureFormat.RGBA32,false);
                cropped.SetPixels(texture.GetPixels(x,y,width,height));
                UnityEngine.Object.Destroy(texture);texture=cropped;
            }
            if(rule!=null && rule.playtest)MarkPlaytest(texture);
            if(crop || rule!=null && rule.playtest)texture.Apply(false,true);
            texture.name="ArcaneForge "+id;
            texture.wrapMode=TextureWrapMode.Clamp;
            Cache[id]=texture;
            return texture;
        }
    }
}
