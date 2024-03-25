using Microsoft.VisualStudio.Debugger.Interop;

namespace HandyTools.Models
{
    public class RefCount<T> where T : class
    {
        public int Count
        {
            get
            {
                lock (instance_)
                {
                    return count_;
                }
            }
        }

        public RefCount(T instance)
        {
            instance_ = instance;
        }

        public void AddRef()
        {
            lock (instance_)
            {
                ++count_;
            }
        }

        public void Release()
        {
            lock (instance_)
            {
                --count_;
            }
        }

        public T Get()
        {
            return instance_;
        }

        private int count_;
        private T instance_;
    }
}
