using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace YaR.Clouds.SpecialCommands;

public abstract class SpecialCommand
{
    protected readonly Cloud _cloud;
    protected readonly string _path;
    protected readonly IList<string> _parameters;

    protected abstract MinMax<int> MinMaxParamsCount { get; }

    protected SpecialCommand(Cloud cloud, string path, IList<string> parameters)
    {
        _cloud = cloud;
        _path = path;
        _parameters = parameters;

        CheckParams();
    }

    public virtual Task<SpecialCommandResult> Execute()
    {
        if (_parameters.Count < MinMaxParamsCount.Min || _parameters.Count > MinMaxParamsCount.Max)
            return Task.FromResult(SpecialCommandResult.Fail);

        return Task.FromResult(SpecialCommandResult.Success);
    }

    private void CheckParams()
    {
        if (_parameters.Count < MinMaxParamsCount.Min || _parameters.Count > MinMaxParamsCount.Max)
            throw new ArgumentException("Invalid parameters count");
    }

}

public readonly struct MinMax<T> where T : IComparable<T>
{
    public MinMax(T min, T max)
    {
        if (min.CompareTo(max) > 0) throw new ArgumentException("min > max");
        Min = min;
        Max = max;
    }

    public MinMax(T one) : this(one, one)
    {
    }

    public T Min { get; }
    public T Max { get; }
}
