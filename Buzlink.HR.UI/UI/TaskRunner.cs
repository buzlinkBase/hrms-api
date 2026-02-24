namespace Buzlink.HR.UI.UI.Common
{
    public class TaskRunner
    {
        public Action<int> handler;
        public TaskRunner(Action<int> handler)
        {
            this.handler = handler;
        }

        public void Looper()
        {
            int i = 0;
            while (i < 100)
            {
                handler(i);
                i += 1;
                System.Threading.Thread.SpinWait(2000);
            }
            //for (int i = 0; i <= 100; )
            //{
            //    handler(i);
            //}
            handler(0);
        }
    }
}
