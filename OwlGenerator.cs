using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Antlr4.StringTemplate;
using Antlr4.StringTemplate.Misc;

internal class OwlGenerator
{
    internal struct OwlProperty
    {
        public Type domain;
        public string relation;
        public Type range;

        public readonly bool IsData => range.IsPrimitive || range == typeof(string);
        public readonly bool IsEnum => range.IsEnum;
        public readonly string[] Enumerators => range.GetEnumNames();

        internal OwlProperty(Type domain, string relation, Type range)
        {
            this.domain = domain;
            this.relation = relation;
            this.range = range;
        }
    }

    internal struct OwlFact // TODO Podia ser um record
    {
        public string relation;
        public string individual;

        internal OwlFact(string relation, string individual)
        {
            this.relation = relation;
            this.individual = individual;
        }
    }

    internal class OwlClass
    {
        public Type Type;
        public OwlClass? SuperClass;

        public string? Comment => Attribute.GetCustomAttributes(Type)
            .OfType<OwlCommentAttribute>()
            .FirstOrDefault()?.value;

        public IEnumerable<Type> DisjointWith => Attribute.GetCustomAttributes(Type)
            .OfType<OwlDisjointWithAttribute>()
            .FirstOrDefault()?.types ?? Enumerable.Empty<Type>();

        internal OwlClass(Type type, OwlClass? superClass)
        {
            Type = type;
            SuperClass = superClass;
        }

        public override string ToString() => Type.Name;
    }

    internal class OwlIndividual
    {
        private object obj;
        public Type type => obj.GetType();
        public string name;
        public List<string> aliases = [];
        public List<OwlFact> facts = [];

        internal OwlIndividual(object obj, string name)
        {
            this.obj = obj;
            this.name = name;
        }

        public override string ToString() => name;
    }

    // TODO Devia ser um URI ou um (uuuuuuuuuurgh) "IRI".
    internal string @namespace = "http://pizza.com";
    internal string comment;

    // TODO MemberInfo não é imutável, por causa do contexto. Não deveríamos estar usando tuplas...
    internal HashSet<OwlProperty> properties = [];

    // TODO Deveria botar ValueType pra excluir
    // mais tipos, mas talvez devesse excluir um
    // monte de tipos que o usuário poderia estender?
    // Ou talvez o usuário devesse colocar um atributo
    // na classe pai que deve "parar"...
    internal Dictionary<Type, OwlClass> classes = [];// [typeof(object), null];

    internal Dictionary<object, OwlIndividual> individuals = [];
    internal HashSet<string> allIndividualNames = [];

    internal OwlGenerator(string @namespace, string comment = "")
    {
        this.@namespace = @namespace;
        this.comment = comment;
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

        individual = new OwlIndividual(obj, UniqueIndividualName(name));
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

    internal OwlClass? AddClass(Type? type)
    {
        Debug.Assert(type != typeof(ValueType));

        if (type == null || type == typeof(object))
            return null;
        if (classes.ContainsKey(type))
            return classes[type];

        OwlClass klass = new OwlClass(type, AddClass(type.BaseType!));
        classes.Add(type, klass);

        // TODO Deveríamos buscar propriedades também, além de atributos
        // TODO Reconsiderar outros BindingFlag, tipo pegar os públicos?
        foreach (var memberInfo in type.FindMembers(MemberTypes.Field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, (m, fc) => true, null))
        {
            if (memberInfo is FieldInfo fieldInfo)
            {
                Type domain = type;

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

        return klass;
    }

    internal void Render(string filename)
    {
        TemplateGroupFile group = new TemplateGroupFile(Environment.CurrentDirectory + "\\OwlTemplate.stg");
        group.Listener = new StringTemplateErrorListener();

        Template template = group.GetInstanceOf("decl");
        template.Add("namespace", @namespace);
        template.Add("comment", comment);
        template.Add("properties", properties);
        template.Add("classes", classes.Values);
        template.Add("individuals", individuals.Values);

        File.WriteAllText($"{filename}.omn", template.Render());
    }

    private string UniqueIndividualName(string name)
    {
        for (int i = 1; i <= 100; i++)
        {
            // O nome do indivíduo é diferente da
            // classe para os links no Protégé
            // irem para os indivíduos
            string uniqueName = $"{name.Replace(' ', '+')}_{i}";
            if (!allIndividualNames.Contains(uniqueName))
            {
                allIndividualNames.Add(uniqueName);
                return uniqueName;
            }
        }
        throw new ArgumentException("Quantidade de indivíduos para classe excedida!", nameof(name));
    }

    internal sealed class StringTemplateErrorListener : ITemplateErrorListener
    {
        public void CompiletimeError(TemplateMessage msg)
        {
            Console.WriteLine($"CompiletimeError: {msg}");
        }

        public void InternalError(TemplateMessage msg)
        {
            Console.WriteLine($"InternalError: {msg}");
        }

        public void IOError(TemplateMessage msg)
        {
            Console.WriteLine($"IOError: {msg}");
        }

        public void RuntimeError(TemplateMessage msg)
        {
            Console.WriteLine($"RuntimeError: {msg}");
        }
    }
}
