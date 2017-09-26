namespace LocalProjections.Tests
{
    using System;
    using System.Threading.Tasks;

    internal static class TaskExtensions
    {
        public static async Task<bool> WithTimeout(this Task task, int timeout = 3000)
        {
            if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
            {
                await task;
                return true;
            }
            return false;
        }
        public static async Task<T> WithTimeout<T>(this Task<T> task, int timeout = 3000)
        {
            if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
            {
                return await task;
            }
            throw new TimeoutException();
        }
    }
}