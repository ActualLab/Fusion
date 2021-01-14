using System;
using Stl.CommandR.Internal;

namespace Stl.CommandR.Commands
{
    public interface IServerSideCommand : IPreprocessedCommand
    {
        void MarkServerSide(bool isServerSide);
    }

    public interface IServerSideCommand<TResult> : IServerSideCommand, ICommand<TResult>
    { }

    public abstract record ServerSideCommandBase<TResult> : IServerSideCommand<TResult>
    {
        [NonSerialized]
        private bool _isServerSide = false;

        public void MarkServerSide(bool isServerSide) => _isServerSide = isServerSide;

        public virtual void Preprocess(CommandContext context)
        {
            if (!_isServerSide)
                throw Errors.CommandIsServerSideOnly();
        }
    }
}
