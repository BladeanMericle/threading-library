namespace Mericle.Threading;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// コールバックの呼び出しが可能なオブジェクトを表すインタフェースです。
/// </summary>
public interface IInvokable
{
    /// <summary>
    /// コールバックを同期的に呼び出します。
    /// </summary>
    /// <param name="callback">コールバック。</param>
    /// <exception cref="InvalidOperationException">コールバックを呼び出せる状態ではありません。</exception>
    /// <exception cref="StackOverflowException">コールバックを実行しているスレッドで一定回数以上再帰的に呼び出されました。</exception>
    /// <exception cref="Exception">コールバックで例外が発生しました。</exception>
    void Invoke(Action callback);

    /// <summary>
    /// コールバックを同期的に呼び出します。
    /// </summary>
    /// <typeparam name="TState">コールバックに渡すオブジェクトの型。</typeparam>
    /// <param name="callback">コールバック。</param>
    /// <param name="state">コールバックに渡すオブジェクト。</param>
    /// <exception cref="InvalidOperationException">コールバックを呼び出せる状態ではありません。</exception>
    /// <exception cref="StackOverflowException">コールバックを実行しているスレッドで一定回数以上再帰的に呼び出されました。</exception>
    /// <exception cref="Exception">コールバックで例外が発生しました。</exception>
    void Invoke<TState>(Action<TState> callback, TState state);

    /// <summary>
    /// コールバックを同期的に呼び出します。
    /// </summary>
    /// <typeparam name="TResult">コールバックの戻り値の型。</typeparam>
    /// <param name="callback">コールバック。</param>
    /// <returns>コールバックの戻り値。</returns>
    /// <exception cref="InvalidOperationException">コールバックを呼び出せる状態ではありません。</exception>
    /// <exception cref="StackOverflowException">コールバックを実行しているスレッドで一定回数以上再帰的に呼び出されました。</exception>
    /// <exception cref="Exception">コールバックで例外が発生しました。</exception>
    [return: MaybeNull]
    TResult Invoke<TResult>(Func<TResult> callback);

    /// <summary>
    /// コールバックを同期的に呼び出します。
    /// </summary>
    /// <typeparam name="TState">コールバックに渡すオブジェクトの型。</typeparam>
    /// <typeparam name="TResult">コールバックの戻り値の型。</typeparam>
    /// <param name="callback">コールバック。</param>
    /// <param name="state">コールバックに渡すオブジェクト。</param>
    /// <returns>コールバックの戻り値。</returns>
    /// <exception cref="InvalidOperationException">コールバックを呼び出せる状態ではありません。</exception>
    /// <exception cref="StackOverflowException">コールバックを実行しているスレッドで一定回数以上再帰的に呼び出されました。</exception>
    /// <exception cref="Exception">コールバックで例外が発生しました。</exception>
    [return: MaybeNull]
    TResult Invoke<TState, TResult>(Func<TState, TResult> callback, TState state);

    /// <summary>
    /// コールバックを非同期に呼び出します。
    /// </summary>
    /// <param name="callback">コールバック。</param>
    /// <exception cref="InvalidOperationException">コールバックを呼び出せる状態ではありません。</exception>
    void InvokeAsync(Action callback);

    /// <summary>
    /// コールバックを非同期に呼び出します。
    /// </summary>
    /// <typeparam name="TState">コールバックに渡すオブジェクトの型。</typeparam>
    /// <param name="callback">コールバック。</param>
    /// <param name="state">コールバックに渡すオブジェクト。</param>
    /// <exception cref="InvalidOperationException">コールバックを呼び出せる状態ではありません。</exception>
    void InvokeAsync<TState>(Action<TState> callback, TState state);
}
