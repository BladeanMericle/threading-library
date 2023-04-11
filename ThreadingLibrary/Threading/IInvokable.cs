namespace Mericle.Threading;

/// <summary>
/// コールバックの呼び出しが可能なオブジェクトを表すインタフェースです。
/// </summary>
public interface IInvokable
{
    /// <summary>
    /// コールバックを同期的に呼び出します。
    /// </summary>
    /// <param name="callback">コールバック。</param>
    void Invoke(Action callback);

    /// <summary>
    /// コールバックを同期的に呼び出します。
    /// </summary>
    /// <typeparam name="TState">コールバックに渡すオブジェクトの型。</typeparam>
    /// <param name="callback">コールバック。</param>
    /// <param name="state">コールバックに渡すオブジェクト。</param>
    void Invoke<TState>(Action<TState?> callback, TState? state);

    /// <summary>
    /// コールバックを同期的に呼び出します。
    /// </summary>
    /// <typeparam name="TResult">コールバックの戻り値の型。</typeparam>
    /// <param name="callback">コールバック。</param>
    /// <returns>コールバックの戻り値。</returns>
    TResult? Invoke<TResult>(Func<TResult?> callback);

    /// <summary>
    /// コールバックを同期的に呼び出します。
    /// </summary>
    /// <typeparam name="TState">コールバックに渡すオブジェクトの型。</typeparam>
    /// <typeparam name="TResult">コールバックの戻り値の型。</typeparam>
    /// <param name="callback">コールバック。</param>
    /// <param name="state">コールバックに渡すオブジェクト。</param>
    /// <returns>コールバックの戻り値。</returns>
    TResult? Invoke<TState, TResult>(Func<TState?, TResult?> callback, TState? state);

    /// <summary>
    /// コールバックを非同期に呼び出します。
    /// </summary>
    /// <param name="callback">コールバック。</param>
    void InvokeAsync(Action callback);

    /// <summary>
    /// コールバックを非同期に呼び出します。
    /// </summary>
    /// <typeparam name="TState">コールバックに渡すオブジェクトの型。</typeparam>
    /// <param name="callback">コールバック。</param>
    /// <param name="state">コールバックに渡すオブジェクト。</param>
    void InvokeAsync<TState>(Action<TState?> callback, TState? state);
}
