using System;
using System.Collections.Generic;
using System.Numerics;

using CSGL.GLFW;
using CSGL.Input;

namespace UnnamedEngine.Input {
    public class Input {
        Window window;
        Queue<KeyEvent> keyEvents;
        HashSet<KeyCode> keyHold;
        HashSet<KeyCode> keyFirstDown;
        HashSet<KeyCode> keyFirstUp;

        Queue<MouseButtonEvent> mouseButtonEvents;
        HashSet<MouseButton> mouseHold;
        HashSet<MouseButton> mouseFirstDown;
        HashSet<MouseButton> mouseFirstUp;

        Queue<MouseEvent> moveEvents;
        Queue<MouseEvent> scrollEvents;
        Queue<TextEvent> textEvents;

        Vector2 mousePos;
        Vector2 mouseDelta;
        Vector2 scrollDelta;

        public event Action<KeyCode, int, KeyAction, KeyMod> OnKey = delegate { };
        public event Action<string> OnText = delegate { };
        public event Action<uint, KeyMod> OnTextMod = delegate { };

        public event Action<Vector2> OnMousePos = delegate { };
        public event Action<MouseButton, KeyAction, KeyMod> OnMouseButton = delegate { };
        
        public event Action<KeyCode> OnKeyDown = delegate { };
        public event Action<KeyCode> OnKeyHold = delegate { };
        public event Action<KeyCode> OnKeyUp = delegate { };

        public event Action<MouseButton> OnMouseButtonDown = delegate { };
        public event Action<MouseButton> OnMouseButtonUp = delegate { };
        public event Action<MouseButton> OnMouseButtonHold = delegate { };

        public event Action<Vector2> OnMouseMoveFine = delegate { };
        public event Action<Vector2> OnMouseMove = delegate { };

        public event Action<Vector2> OnScrollFine = delegate { };
        public event Action<Vector2> OnScroll = delegate { };

        public Vector2 MouseDelta {
            get {
                return mouseDelta;
            }
        }

        public Vector2 ScrollDelta {
            get {
                return scrollDelta;
            }
        }

        public Vector2 MousePos {
            get {
                return mousePos;
            }
        }

        public Input(Window window) {
            if (window == null) throw new ArgumentNullException(nameof(window));

            this.window = window;

            keyEvents = new Queue<KeyEvent>();
            keyHold = new HashSet<KeyCode>();
            keyFirstDown = new HashSet<KeyCode>();
            keyFirstUp = new HashSet<KeyCode>();

            mouseButtonEvents = new Queue<MouseButtonEvent>();
            mouseHold = new HashSet<MouseButton>();
            mouseFirstDown = new HashSet<MouseButton>();
            mouseFirstUp = new HashSet<MouseButton>();

            moveEvents = new Queue<MouseEvent>();
            scrollEvents = new Queue<MouseEvent>();

            window.OnKey += Key;
            window.OnTextMod += Text;
            window.OnCursorPos += CursorPos;
            window.OnMouseButton += MouseButton;
            window.OnScroll += Scroll;
        }

        void Key(KeyCode code, int scanCode, KeyAction action, KeyMod modifiers) {
            lock (keyEvents) keyEvents.Enqueue(new KeyEvent(code, scanCode, action, modifiers));
        }

        void Text(uint codepoint, KeyMod modifiers) {
            lock (textEvents) textEvents.Enqueue(new TextEvent(codepoint, modifiers));
        }

        void CursorPos(double x, double y) {
            lock (moveEvents) moveEvents.Enqueue(new MouseEvent(x, y));
        }

        void MouseButton(MouseButton button, KeyAction action, KeyMod mod) {
            lock (mouseButtonEvents) mouseButtonEvents.Enqueue(new MouseButtonEvent(button, action, mod));
        }

        void Scroll(double x, double y) {
            lock (scrollEvents) scrollEvents.Enqueue(new MouseEvent(x, y));
        }
        
        internal void Update() {
            keyFirstDown.Clear();
            keyFirstUp.Clear();
            mouseFirstDown.Clear();
            mouseFirstUp.Clear();

            lock (keyEvents) {
                while (keyEvents.Count > 0) {
                    KeyEvent e = keyEvents.Dequeue();

                    OnKey(e.code, e.scan, e.action, e.mod);

                    if (e.action == KeyAction.Press) {
                        keyFirstDown.Add(e.code);
                        keyHold.Add(e.code);
                        OnKeyDown(e.code);
                    } else if (e.action == KeyAction.Release) {
                        keyFirstUp.Add(e.code);
                        keyHold.Remove(e.code);
                        OnKeyUp(e.code);
                    }
                }
            }

            lock (mouseButtonEvents) {
                while (mouseButtonEvents.Count > 0) {
                    MouseButtonEvent e = mouseButtonEvents.Dequeue();

                    OnMouseButton(e.button, e.action, e.mod);

                    if (e.action == KeyAction.Press) {
                        mouseFirstDown.Add(e.button);
                        mouseHold.Add(e.button);
                        OnMouseButtonDown(e.button);
                    } else if (e.action == KeyAction.Release) {
                        mouseFirstUp.Add(e.button);
                        mouseHold.Remove(e.button);
                        OnMouseButtonUp(e.button);
                    }
                }
            }

            foreach (var k in keyHold) {
                OnKeyHold(k);
            }

            foreach (var m in mouseHold) {
                OnMouseButtonHold(m);
            }

            lock (moveEvents) {
                mouseDelta = new Vector2();

                while (moveEvents.Count > 0) {
                    MouseEvent e = moveEvents.Dequeue();
                    Vector2 pos = new Vector2((float)e.x, (float)e.y);
                    Vector2 delta = pos - mousePos;

                    mousePos = pos;
                    mouseDelta += delta;

                    OnMousePos(pos);
                    OnMouseMoveFine(delta);
                }

                if (mouseDelta != Vector2.Zero) {
                    OnMouseMove(mouseDelta);
                }
            }

            lock (scrollEvents) {
                scrollDelta = new Vector2();

                while (scrollEvents.Count > 0) {
                    MouseEvent e = scrollEvents.Dequeue();
                    Vector2 delta = new Vector2((float)e.x, (float)e.y);
                    scrollDelta += delta;

                    OnScrollFine(delta);
                }
                if (scrollDelta != Vector2.Zero) {
                    OnScroll(scrollDelta);
                }
            }
        }

        public bool FirstDown(KeyCode key) {
            return keyFirstDown.Contains(key);
        }

        public bool Hold(KeyCode key) {
            return keyHold.Contains(key);
        }

        public bool FirstUp(KeyCode key) {
            return keyFirstUp.Contains(key);
        }

        public bool FirstDown(MouseButton button) {
            return mouseFirstDown.Contains(button);
        }

        public bool Hold(MouseButton button) {
            return mouseHold.Contains(button);
        }

        public bool FirstUp(MouseButton button) {
            return mouseFirstUp.Contains(button);
        }
    }

    struct KeyEvent {
        public KeyCode code;
        public int scan;
        public KeyAction action;
        public KeyMod mod;

        public KeyEvent(KeyCode code, int scanCode, KeyAction action, KeyMod mod) {
            this.code = code;
            this.scan = scanCode;
            this.action = action;
            this.mod = mod;
        }
    }

    struct MouseButtonEvent {
        public MouseButton button;
        public KeyAction action;
        public KeyMod mod;

        public MouseButtonEvent(MouseButton button, KeyAction action, KeyMod mod) {
            this.button = button;
            this.action = action;
            this.mod = mod;
        }
    }

    struct MouseEvent {
        public double x, y;

        public MouseEvent(double x, double y) {
            this.x = x;
            this.y = y;
        }
    }

    struct TextEvent {
        public uint codepoint;
        public KeyMod mod;

        public TextEvent(uint codepoint, KeyMod mod) {
            this.codepoint = codepoint;
            this.mod = mod;
        }
    }
}
