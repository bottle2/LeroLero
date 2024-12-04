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

        public override readonly string? ToString()
        {
            if (value is float f)
            {
                return f.ToString(numberFormatInfo);
            }
            else if (value is double d)
            {
                return d.ToString(numberFormatInfo);
            }
            else if (value is decimal dec)
            {
                return dec.ToString(numberFormatInfo);
            }
            else
            {
                return value.ToString();
            }
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

    internal struct OwlFact
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

    internal Uri ontology;
    internal string comment;

    internal HashSet<OwlProperty> properties = [];
    internal Dictionary<Type, OwlClass> classes = [];
    internal Dictionary<object, OwlIndividual> individuals = [];
    internal HashSet<string> allIndividualNames = [];

    internal Regex backingFieldRegex = new(@"<([^>]+)>k__BackingField");

    internal OwlGenerator(string ontology, string comment = "")
    {
        this.ontology = new Uri(ontology);
        this.comment = comment;
    }

    internal OwlIndividual AddIndividual(object obj)
    {
        string name = FindIndividualName(obj)?.Replace(' ', '+') ?? UniqueIndividualName(obj.GetType().Name);
        return AddIndividual(obj, name);
    }

    internal OwlIndividual AddIndividual(object obj, string name)
    {
        OwlIndividual? individual;
        if (individuals.TryGetValue(obj, out individual))
        {
            if (!individual.aliases.Contains(name))
                individual.aliases.Add(name);
            return individual;
        }

        AddClass(obj.GetType());

        individual = new OwlIndividual(obj, name);
        individuals.Add(obj, individual);

        IEnumerable<FieldInfo> individualFields = obj.GetType()
            .FindMembers(MemberTypes.Field, BindingFlags.Instance | BindingFlags.NonPublic, (m, fc) => true, null)
            .OfType<FieldInfo>();

        foreach (FieldInfo fieldInfo in individualFields)
        {
            string relation = GetPropertyName(fieldInfo.Name);
            var fieldValue = fieldInfo.GetValue(obj)!;

            if (fieldValue is not string && fieldValue is IEnumerable fieldEnumerable)
            {
                foreach (object fieldIndividual in fieldEnumerable)
                {
                    individual.facts.Add(new OwlFact(relation, AddIndividual(fieldIndividual)));
                }
            }
            else
            {
                individual.facts.Add(new OwlFact(relation, fieldValue));
            }
        }

        return individual;
    }

    internal OwlClass? AddClass(Type? type)
    {
        Debug.Assert(type != typeof(ValueType));

        if (type == null || type == typeof(object))
        {
            return null;
        }

        if (classes.ContainsKey(type))
        {
            return classes[type];
        }

        OwlClass klass = new OwlClass(type, AddClass(type.BaseType!));
        classes.Add(type, klass);

        IEnumerable<FieldInfo> classFields = type
            .FindMembers(MemberTypes.Field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, (m, fc) => true, null)
            .OfType<FieldInfo>();

        foreach (FieldInfo fieldInfo in classFields)
        {
            Type domain = type;
            string relation = GetPropertyName(fieldInfo.Name);
            Type range = fieldInfo.FieldType;

            if (range != typeof(string) && fieldInfo.FieldType.IsAssignableTo(typeof(IEnumerable)))
            {
                range = fieldInfo.FieldType.GetElementType()!;
            }

            OwlProperty property = new OwlProperty(domain, relation, range);
            klass.Properties.Add(property);
            properties.Add(property);
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

    static string? FindIndividualName(object obj)
    {
        MemberInfo? memberInfo = obj.GetType()
            .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(prop => Attribute.IsDefined(prop, typeof(OwlIndividualNameAttribute)));

        if (memberInfo != null)
        {
            if (memberInfo is PropertyInfo property && property.GetValue(obj) is string propertyName)
            {
                return propertyName;
            }
            else if (memberInfo is FieldInfo field && field.GetValue(obj) is string fieldName)
            {
                return fieldName;
            }
        }

        return null;
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
