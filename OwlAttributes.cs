﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

internal class OwlIndividualNameAttribute : Attribute { }

internal class OwlCommentAttribute : Attribute
{
    internal string value;

    internal OwlCommentAttribute(string value)
    {
        this.value = value;
    }
}

internal class OwlDisjointWithAttribute : Attribute
{
    internal Type[] types;

    internal OwlDisjointWithAttribute(params Type[] types)
    {
        this.types = types;
    }
}
