using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

internal struct OwlExportAttribute { }
internal class OwlIndividualNameAttribute : Attribute { }

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


internal class OwlGenerator
{
    internal struct OwlProperty
    {
        internal Type domain;
        internal string relation;
        internal Type range;

        internal OwlProperty(Type domain, string relation, Type range)
        {
            this.domain = domain;
            this.relation = relation;
            this.range = range;
        }
    }

    internal struct OwlFact // TODO Podia ser um record
    {
        internal string relation;
        internal string individual;

        internal OwlFact(string relation, string individual)
        {
            this.relation = relation;
            this.individual = individual;
        }
    }

    internal class OwlIndividual
    {
        internal List<string> aliases = [];
        internal List<OwlFact> facts = [];

        internal string name => aliases[0];

        internal OwlIndividual(string name)
        {
            this.aliases = [name];
        }
    }

    // TODO Devia ser um URI ou um (uuuuuuuuuurgh) "IRI".
    internal string @namespace = "http://pizza.com";

    // TODO MemberInfo não é imutável, por causa do contexto. Não deveríamos estar usando tuplas...
    internal HashSet<OwlProperty> properties = [];

    // TODO Deveria botar ValueType pra excluir
    // mais tipos, mas talvez devesse excluir um
    // monte de tipos que o usuário poderia estender?
    // Ou talvez o usuário devesse colocar um atributo
    // na classe pai que deve "parar"...
    internal HashSet<Type> classes = [typeof(object), null];

    internal Dictionary<object, OwlIndividual> individuals = [];
    internal HashSet<string> allIndividualNames = [];

    internal OwlGenerator(string @namespace)
    {
        this.@namespace = @namespace;
    }

    internal string AddIndividual(object obj)
    {
        // TODO Search for name using reflection.
        return AddIndividual(obj, FindName(obj)?.Replace(' ', '+') ?? obj.GetType().Name);

        static string? FindName(object obj)
        {
            if (
                obj.GetType()
                   .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                   .FirstOrDefault(prop => Attribute.IsDefined(prop, typeof(OwlIndividualNameAttribute)))
                   is MemberInfo mi) // TODO Sei lá como formatar essa joça.
            {
                if (mi is PropertyInfo pi && pi.GetValue(obj) is string name0)
                    return name0;
                else if (mi is FieldInfo fi && fi.GetValue(obj) is string name)
                    return name;
            }
            return null;
        }
    }

    internal string AddIndividual(object obj, string name)
    {
        OwlIndividual? individual;
        if (individuals.TryGetValue(obj, out individual))
        {
            if (individual.aliases.Contains(name))
                return name;

            individual.aliases.Add(UniqueIndividualName(name));
            return individual.name;
        }

        AddClass(obj.GetType());

        individual = new OwlIndividual(UniqueIndividualName(name));
        individuals.Add(obj, individual);

        foreach (var memberInfo in obj.GetType().FindMembers(MemberTypes.Field, BindingFlags.Instance | BindingFlags.NonPublic, (m, fc) => true, null))
        {
            if (memberInfo is FieldInfo fieldInfo)
            {
                // TODO Código duplicado
                string nameson = fieldInfo.Name;
                Regex match = new(@"<([^>]+)>k__BackingField");
                Match match2 = match.Match(nameson);
                if (match2.Success)
                    nameson = match2.Groups[1].Value;

                if (fieldInfo.FieldType.IsPrimitive || fieldInfo.FieldType == typeof(string))
                {
                    string? tits = null;
                    if (fieldInfo.FieldType == typeof(string))
                        tits = $"\"{(string)fieldInfo.GetValue(obj)!}\"";
                    else if (fieldInfo.FieldType == typeof(bool))
                        tits = (bool)fieldInfo.GetValue(obj)! ? "true" : "false";
                    else if (fieldInfo.FieldType == typeof(float) || fieldInfo.FieldType == typeof(double) || fieldInfo.FieldType == typeof(decimal))
                    {
                        NumberFormatInfo nfi = new();
                        nfi.NumberDecimalSeparator = ".";
                        tits = (string)fieldInfo.FieldType.InvokeMember("ToString", BindingFlags.InvokeMethod, null, fieldInfo.GetValue(obj), new object[] { nfi })!;
                        // TODO Acabou o clean code.
                        if (fieldInfo.FieldType == typeof(float))
                            tits += "f";
                    }
                    else
                        tits = fieldInfo.GetValue(obj)!.ToString();
                    individual.facts.Add(new OwlFact(nameson, tits!));
                }
                else if (fieldInfo.FieldType.IsEnum)
                    individual.facts.Add(new OwlFact(nameson, $"\"{fieldInfo.GetValue(obj)!}\""));
                else
                {
                    var fieldValue = fieldInfo.GetValue(obj);
                    // XXX Ontologias não tem listas
                    //     Então repetimos várias vezes a mesma relação!
                    if (fieldValue!.GetType().IsAssignableTo(typeof(IEnumerable)))
                    {
                        foreach (object fieldIndividual in (IEnumerable)fieldValue)
                            individual.facts.Add(new OwlFact($"has{fieldInfo.Name}", AddIndividual(fieldIndividual)));
                    }
                }
            }
        }

        return individual.name;
    }

    internal void AddClass(Type @class)
    {
        Debug.Assert(@class != typeof(ValueType));

        if (classes.Contains(@class))
            return;

        classes.Add(@class);

        // TODO Deveríamos buscar propriedades também, além de atributos
        // TODO Reconsiderar outros BindingFlag, tipo pegar os públicos?
        foreach (var memberInfo in @class.FindMembers(MemberTypes.Field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, (m, fc) => true, null))
        {
            if (memberInfo is FieldInfo fieldInfo)
            {
                Type domain = @class;

                // TODO Código duplicado
                string nameson = fieldInfo.Name;
                Regex match = new(@"<([^>]+)>k__BackingField");
                Match match2 = match.Match(nameson);
                if (match2.Success)
                    nameson = match2.Groups[1].Value;

                // TODO Nem sei se isso funciona! Só tava preocupado com Topping[] quando escrevi.
                // TODO Ver se funciona com coisas que não são descendentes de IEnumerable, como um Dictionary
                Type range = fieldInfo.FieldType;

                if (!range.IsPrimitive && range != typeof(string)) // TODO Código duplicado
                {
                    range = fieldInfo.FieldType.IsAssignableTo(typeof(IEnumerable)) ? fieldInfo.FieldType.GetElementType()! : fieldInfo.FieldType!;
                }
                properties.Add(new OwlProperty(domain, nameson, range));
            }
        }

        AddClass(@class.BaseType!);
    }

    internal void Render(string filename)
    {
        // TODO E o caminho pra salvar? Atualmente vai pra pasta do projeto/bin/Debug/net8.0/
        using StreamWriter sw = new($"{filename}.omn", false, System.Text.Encoding.UTF8);
        sw.WriteLine($"Prefix: : <{@namespace}/>");

        if (properties.Any(p => p.range.IsPrimitive || p.range == typeof(string))) // TODO Eca que nooojo.
            sw.WriteLine("Prefix: xsd: <http://www.w3.org/2001/XMLSchema#>");

        sw.WriteLine();
        sw.WriteLine($"Ontology: <{@namespace}>");
        sw.WriteLine();

        foreach (OwlProperty property in properties)
        {
            if (property.range.IsPrimitive || property.range == typeof(string)) // TODO Método de extensão?
            {
                sw.WriteLine($"DataProperty: {property.relation}");
                sw.WriteLine($"  Domain:    {property.domain.Name}");
                if (property.range == typeof(string))
                    sw.WriteLine("  Range:     xsd:string");
                else if (property.range == typeof(sbyte))
                    sw.WriteLine("  Range:     xsd:byte");
                else if (property.range == typeof(byte))
                    sw.WriteLine("  Range:     xsd:unsignedByte");
                else if (property.range == typeof(short))
                    sw.WriteLine("  Range:     xsd:short");
                else if (property.range == typeof(ushort))
                    sw.WriteLine("  Range:     xsd:unsignedShort");
                else if (property.range == typeof(int))
                    sw.WriteLine("  Range:     xsd:int");
                else if (property.range == typeof(uint))
                    sw.WriteLine("  Range:     xsd:unsignedInt");
                else if (property.range == typeof(long))
                    sw.WriteLine("  Range:     xsd:long");
                else if (property.range == typeof(ulong))
                    sw.WriteLine("  Range:     xsd:unsignedLong");
                else if (property.range == typeof(char))
                    sw.WriteLine("  Range:     xsd:byte");
                else if (property.range == typeof(float))
                    sw.WriteLine("  Range:     xsd:float");
                else if (property.range == typeof(double))
                    sw.WriteLine("  Range:     xsd:double");
                else if (property.range == typeof(decimal))
                    sw.WriteLine("  Range:     xsd:decimal");
                else if (property.range == typeof(bool))
                    sw.WriteLine("  Range:     xsd:boolean");
                else
                    Debug.Assert("Paniquei!!!!" == null);
                // TODO Maníacos do Rust, Swift e programação funcional dirão que esse código não tem nada de errado.
            }
            else if (property.range.IsEnum)
            {
                sw.WriteLine($"DataProperty: {property.relation}");
                sw.WriteLine($"  Domain:    {property.domain.Name}");
                sw.WriteLine($"  Range:     {{\"{string.Join("\", \"", property.range.GetEnumNames())}\"}}");
            }
            else
            {
                // TODO Essa coisa de has{} e is{]Of é claramente uma gambiarra
                //      Criar uma anotação pro usuário dizer o nome das relações inversas, caso sejam inversas sequer!
                sw.WriteLine($"ObjectProperty: has{property.relation}");
                sw.WriteLine($"  Domain:    {property.domain.Name}");
                sw.WriteLine($"  Range:     {property.range.Name}");
                sw.WriteLine($"  InverseOf: is{property.relation}Of");
                sw.WriteLine();
                sw.WriteLine($"ObjectProperty: is{property.relation}Of");
                sw.WriteLine();
            }
        }

        foreach (Type @class in classes)
        {
            if (@class == null || @class == typeof(object))
                continue;
            sw.WriteLine($"Class: {@class.Name}");
            if (@class.BaseType != typeof(object))
                sw.WriteLine($"  SubClassOf: {@class.BaseType!.Name}");
            foreach (var attribute in Attribute.GetCustomAttributes(@class))
            {
                if (attribute is OwlDisjointWithAttribute disjointWith)
                {
                    foreach (Type type in disjointWith.types)
                        sw.WriteLine($"  DisjointWith: {type.Name}");
                }
            }
            sw.WriteLine();
        }

        foreach ((object obj, OwlIndividual individual) in individuals)
        {
            sw.WriteLine($"Individual: {individual.name}");
            sw.WriteLine($"  Types: {obj.GetType().Name}");
            foreach (OwlFact fact in individual.facts)
                sw.WriteLine($"  Facts: {fact.relation} {fact.individual}");
            sw.WriteLine();
            for (int i = 1; i < individual.aliases.Count; i++)
            {
                sw.WriteLine($"Individual: {individual.aliases[i]}");
                sw.WriteLine($"  SameAs: {individual.name}");
                sw.WriteLine();
            }
        }
    }

    private string UniqueIndividualName(string name)
    {
        for (int i = 1; i <= 100; i++)
        {
            // O nome do indivíduo é diferente da
            // classe para os links no Protégé
            // irem para os indivíduos
            string uniqueName = $"{name}_{i}";
            if (!allIndividualNames.Contains(uniqueName))
            {
                allIndividualNames.Add(uniqueName);
                return uniqueName;
            }
        }
        throw new ArgumentException("Quantidade de indivíduos para classe excedida!", nameof(name));
    }
}
