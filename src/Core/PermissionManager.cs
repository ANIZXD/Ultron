using System;

namespace Ultron.Core
{
    /// <summary>
    /// Explicit permission gate. Ultron cannot speak unless the state is Approved.
    /// </summary>
    public class PermissionManager
    {
        private readonly object _gate = new object();
        private PermissionState _state;

        public event EventHandler<PermissionState> StateChanged;

        public PermissionManager()
        {
            _state = PermissionState.Locked;
        }

        public PermissionState State
        {
            get
            {
                lock (_gate)
                {
                    return _state;
                }
            }
        }

        public bool CanSpeak
        {
            get { return State == PermissionState.Approved; }
        }

        public bool RequestStart()
        {
            return Transition(PermissionState.Pending, PermissionState.Locked, PermissionState.Revoked);
        }

        public bool Approve()
        {
            return Transition(PermissionState.Approved, PermissionState.Pending);
        }

        public bool Decline()
        {
            return Transition(PermissionState.Locked, PermissionState.Pending);
        }

        public bool Revoke()
        {
            return Transition(PermissionState.Revoked, PermissionState.Approved, PermissionState.Pending);
        }

        public void Reset()
        {
            SetState(PermissionState.Locked);
        }

        private bool Transition(PermissionState next, params PermissionState[] allowed)
        {
            bool changed = false;
            lock (_gate)
            {
                for (int i = 0; i < allowed.Length; i++)
                {
                    if (_state == allowed[i])
                    {
                        _state = next;
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
            {
                RaiseChanged(next);
            }
            return changed;
        }

        private void SetState(PermissionState next)
        {
            bool changed;
            lock (_gate)
            {
                changed = _state != next;
                _state = next;
            }

            if (changed)
            {
                RaiseChanged(next);
            }
        }

        private void RaiseChanged(PermissionState state)
        {
            EventHandler<PermissionState> handler = StateChanged;
            if (handler != null)
            {
                handler(this, state);
            }
        }
    }
}
