using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

public static class AsyncOperationExtensions
{
    public static TaskAwaiter GetAwaiter(this AsyncOperation asyncOp)
    {
        var tcs = new TaskCompletionSource<bool>();
        asyncOp.completed += _ => tcs.SetResult(true);
        return ((Task)tcs.Task).GetAwaiter();
    }
}
