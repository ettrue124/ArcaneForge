namespace ArcaneForge {
    // Reserved dynamic ability IDs encode a display-only signed loyalty cost.
    public static class LoyaltyDisplay {
        public static string Cost(uint id) {
            int value=(int)(id&255)-128;
            return value>0?"+"+value:value.ToString();
        }
    }
}
