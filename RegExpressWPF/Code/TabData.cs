﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace RegExpressWPF.Code
{
    public class TabData
    {
        public string Name;
        public string Pattern;
        public string Text;
        public RegexOptions RegexOptions;
        public bool ShowFirstMatchOnly;
        public bool ShowCaptures;
        public string Eol;
    }
}
