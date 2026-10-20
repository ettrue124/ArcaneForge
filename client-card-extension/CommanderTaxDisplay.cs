using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ArcaneForge {
    // Add the authoritative tax to the existing commander badge only.
    public static class CommanderTaxDisplay {
        static readonly ConditionalWeakTable<object,object> commanderBadges=new ConditionalWeakTable<object,object>();
        static bool reported;
        public static object Mark(object badge,object designation) {
            if(badge!=null && designation?.ToString()=="Commander")
                commanderBadges.GetValue(badge,_=>new object());
            return badge;
        }
        public static object Apply(object view,object badge,object model) {
            if(view==null || badge==null || model==null || !commanderBadges.TryGetValue(badge,out _))return view;
            try {
                var instance=model.GetType().GetProperty("Instance")?.GetValue(model);
                var designations=instance?.GetType().GetField("Designations")?.GetValue(instance) as IEnumerable;
                if(designations==null)return view;
                foreach(var designation in designations) {
                    var type=designation.GetType();
                    if(type.GetField("Type")?.GetValue(designation)?.ToString()!="Commander")continue;
                    int tax=Convert.ToInt32(type.GetField("CostIncrease").GetValue(designation));
                    view.GetType().GetMethod("SetText",new[]{typeof(string)})?.Invoke(view,new object[]{tax>0?"+"+tax:""});
                    break;
                }
            } catch(Exception error) {
                if(!reported){reported=true;Debug.LogWarning("ArcaneForge commander tax display: "+error.Message);}
            }
            return view;
        }
    }
}
