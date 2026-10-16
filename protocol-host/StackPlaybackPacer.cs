// Keep each automatically resolved stack step visible. Never advance Forge
// during the pause; Full Control/settings can cancel automatic passing.
sealed class StackPlaybackPacer
{
    long button;
    long readyAt;
    public bool Pending => System.Threading.Volatile.Read(ref button)!=0;
    public bool Ready(long currentButton,bool onStack,long now,int delayMs=150)
    {
        if(!onStack || currentButton==0) {Reset();return true;}
        if(button!=currentButton) {button=currentButton;readyAt=now+delayMs;return false;}
        if(now<readyAt)return false;
        return true; // The sender resets only after Forge accepts the pass.
    }
    public void Reset() {button=0;readyAt=0;}
}
