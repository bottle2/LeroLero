using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

internal struct OwlExportAttribute { }

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
    // TODO Gerar erro se a classe que possui
    // o atributo estiver incluída na lista
    internal Type[] types;

    internal OwlDisjointWithAttribute(params Type[] types)
    {
        this.types = types;
    }
}
