using System;
using System.Collections.Generic;

namespace UnnamedEngine.UI {
    public class UIRoot : UIElement {
        public Screen Screen { get; private set; }

        internal UIRoot(Screen screen) {
            Screen = screen;
        }
    }
}
