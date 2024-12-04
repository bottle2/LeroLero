using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Antlr4.StringTemplate;
using Antlr4.StringTemplate.Misc;

internal class OwlGenerator
{
    internal struct OwlValue
    {
        public object value;

        public readonly bool IsString => value is string;
        public readonly bool IsEnum => value.GetType().IsEnum;
        public readonly bool IsBool => value is bool;
        public readonly bool IsFloat => value is float;

        private static NumberFormatInfo numberFormatInfo = new() { NumberDecimalSeparator = "." };

        internal OwlValue(object value)
        {
            this.value = value;
        }

        public override readonly string ToString()
        {
            if (value is float f)
                return f.ToString(numberFormatInfo);
            else if (value is double d)
                return d.ToString(numberFormatInfo);
            else if (value is decimal dec)
                return dec.ToString(numberFormatInfo);
            return value.ToString();
        }
    }

    internal struct OwlType
    {
        public Type type;

        public readonly bool IsData => type.IsPrimitive || type.IsEnum || type == typeof(string);
        public readonly bool IsEnum => type.IsEnum;
        public readonly string[] Enumerators => type.GetEnumNames();

        internal OwlType(Type type)
        {
            this.type = type;
        }

        public override string ToString() => type.Name;
    }

    internal struct OwlProperty
    {
        public OwlType domain;
        public string relation;
        public OwlType range;

        internal OwlProperty(Type domain, string relation, Type range)
        {
            this.domain = new OwlType(domain);
            this.relation = relation;
            this.range = new OwlType(range);
        }

        public override string ToString() => relation;
    }

    internal struct OwlFact // TODO Podia ser um record
    {
        public string relation;
        public OwlValue value;

        internal OwlFact(string relation, object value)
        {
            this.relation = relation;
            this.value = new OwlValue(value);
        }

        public override string ToString() => relation;
    }

    internal class OwlClass
    {
        public Type Type;
        public OwlClass? SuperClass;
        public HashSet<OwlProperty> Properties = [];

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
    internal string ontology = "http://pizza.com";
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

    internal Regex backingFieldRegex = new(@"<([^>]+)>k__BackingField");

    internal OwlGenerator(string ontology, string comment = "")
    {
        this.ontology = ontology;
        this.comment = comment;
    }

    internal OwlIndividual AddIndividual(object obj)
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

    internal OwlIndividual AddIndividual(object obj, string name)
    {
        OwlIndividual? individual;
        if (individuals.TryGetValue(obj, out individual))
        {
            if (individual.aliases.Contains(name))
                return individual;

            individual.aliases.Add(UniqueIndividualName(name));
            return individual;
        }

        AddClass(obj.GetType());

        individual = new OwlIndividual(obj, UniqueIndividualName(name));
        individuals.Add(obj, individual);

        foreach (var memberInfo in obj.GetType().FindMembers(MemberTypes.Field, BindingFlags.Instance | BindingFlags.NonPublic, (m, fc) => true, null))
        {
            if (memberInfo is FieldInfo fieldInfo)
            {
                string relation = GetPropertyName(fieldInfo.Name);

                var fieldValue = fieldInfo.GetValue(obj)!;
                if (fieldInfo.FieldType != typeof(string) && fieldInfo.FieldType.IsAssignableTo(typeof(IEnumerable)))
                {
                    foreach (object fieldIndividual in (IEnumerable)fieldValue)
                        individual.facts.Add(new OwlFact(relation, AddIndividual(fieldIndividual)));
                }
                else
                {
                    individual.facts.Add(new OwlFact(relation, fieldValue));
                }
            }
        }

        return individual;
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
                string relation = GetPropertyName(fieldInfo.Name);
                Type range = fieldInfo.FieldType;

                if (!range.IsPrimitive && range != typeof(string)) // TODO Código duplicado
                {
                    range = fieldInfo.FieldType.IsAssignableTo(typeof(IEnumerable)) ? fieldInfo.FieldType.GetElementType()! : fieldInfo.FieldType!;
                }

                OwlProperty property = new OwlProperty(domain, relation, range);
                klass.Properties.Add(property);
                properties.Add(property);
            }
        }

        return klass;
    }

    internal void Render(string filename)
    {
        TemplateGroupFile group = new TemplateGroupFile(Environment.CurrentDirectory + "\\OwlTemplate.stg");
        group.Listener = new StringTemplateErrorListener();

        Template template = group.GetInstanceOf("decl");
        template.Add("ontology", ontology);
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

    private string GetPropertyName(string name)
    {
        Match match = backingFieldRegex.Match(name);
        return match.Success ? match.Groups[1].Value : name;
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
