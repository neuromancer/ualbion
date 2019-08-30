﻿using System.Collections.Generic;
using UAlbion.Core.Events;

namespace UAlbion.Core
{
    public class SceneStack : Component
    {
        static readonly Handler[] Handlers =
        {
            new Handler<SceneStack, PushSceneEvent>((x, e) =>
            {
                x._stack.Push(x._sceneId);
                x._sceneId = e.SceneId;
                x.Raise(new SetSceneEvent(e.SceneId));
            }),
            new Handler<SceneStack, PopSceneEvent>((x, e) =>
            {
                if (x._stack.Count > 0)
                {
                    var newSceneId = x._stack.Pop();
                    x._sceneId = newSceneId;
                    x.Raise(new SetSceneEvent(newSceneId));
                }
            }),
            new Handler<SceneStack, SetSceneEvent>((x, e) =>
            {
                x._stack.Clear();
                x._sceneId = e.SceneId;
            })
        };

        readonly Stack<int> _stack = new Stack<int>();
        int _sceneId = 0;
        public SceneStack() : base(Handlers) { }
    }
}