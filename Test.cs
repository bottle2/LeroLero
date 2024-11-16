using System.Collections;
using System.Reflection;

// XXX Coisas parecidas?
// https://github.com/dotnetrdf/dotnetrdf/discussions/594
// - https://github.com/giacomociti/iride
// - https://github.com/EcoStruxure/OLGA
// - https://github.com/ukparliament/Mapping

internal struct OwlExportAttribute { }

internal class Program
{
    static void Main()
    {
        // TODO Devia ser um URI ou um (uuuuuuuuuurgh) "IRI".
        string @namespace = "http://pizza.com";
        // TODO E o caminho pra salvar? Atualmente vai pra pasta do projeto/bin/Debug/net8.0/
        string filename = "pizza";

        // TODO Deveria ir para uma classe que cuida da reflex�o
        using StreamWriter sw = new($"{filename}.omn", false, System.Text.Encoding.UTF8);
        sw.WriteLine($"Prefix: : <{@namespace}/>");
        sw.WriteLine($"Ontology: <{@namespace}>");

        // TODO Arrancar fora esssas cole��es e botar dentro de uma classe de inst�ncia.
        Dictionary<object, List<string>> individuals = [];
        HashSet<string> allIndividualNames = [];
        HashSet<Type?> classes = [typeof(object), null]; // TODO Deveria botar ValueType pra excluir
                                                         // mais tipos, mas talvez devesse excluir um
                                                         // monte de tipos que o usu�rio poderia estender?
                                                         // Ou talvez o usu�rio devesse colocar um atributo
                                                         // na classe pai que deve "parar"...

        // TODO MemberInfo n�o � imut�vel, por causa do contexto. N�o dever�amos estar usando tuplas...
        HashSet<(Type, string, Type)> properties = [];

        Traverse(new PizzaMushroom(), "mushroomPizza");
        Traverse(new Margherita(), "margheritaPizza");

        // TODO Deveria ir para uma classe que cuida da reflex�o
        string Traverse(object o, string name)
        {
            if (individuals.TryGetValue(o, out List<string> aliases))
            {
                if (aliases.Contains(name))
                    return name;
                else
                {
                    aliases.Add(UniqueIndividualName(name));
                    sw.WriteLine($"Individual: {aliases[^1]}");
                    sw.WriteLine($"  SameAs: {aliases[0]}");
                    return aliases[0];
                }
            }
            else
            {
                for (
                    Type? current = o.GetType(), parent = current.BaseType;
                    !classes.Contains(current);
                    classes.Add(current), current = parent, parent = parent.BaseType
                ) {
                    sw.WriteLine($"Class: {current.Name}");
                    if (parent != typeof(object))
                        sw.WriteLine($"  SubClassOf: {parent!.Name}");

                    // TODO Dever�amos buscar propriedades tamb�m, al�m de atributos
                    // TODO Reconsiderar outros BindingFlag, tipo pegar os p�blicos?
                    foreach (var p in current.FindMembers(MemberTypes.Field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, (m, fc) => true, null))
                    {
                        if (p is FieldInfo fi)
                        {
                            Type domain = current;
                            string relation = p.Name;
                            // TODO Nem sei se isso funciona! S� tava preocupado com Topping[] quando escrevi.
                            // TODO Se o tipo for n�o gerenciado como inteiro ou string, deveria ser um "DataProperty" ao inv�s de "ObjectPropert" no Manchester gerado
                            // TODO Ver se funciona com coisas que n�o s�o descendentes de IEnumerable, como um atributo comum ou um Dictionary
                            Type range = fi.FieldType.IsAssignableTo(typeof(IEnumerable)) ? fi.FieldType.GetElementType() : fi.FieldType;

                            if (properties.Add((domain, relation, range)))
                            {
                                // TODO Essa coisa de has{} e is{]Of � claramente uma gambiarra
                                //      Criar uma anota��o pro usu�rio dizer o nome das rela��es inversas, caso sejam inversas sequer!
                                sw.WriteLine($"ObjectProperty: has{p.Name}");
                                sw.WriteLine($"  Domain:    {domain.Name}");
                                sw.WriteLine($"  Range:     {range.Name}");
                                sw.WriteLine($"  InverseOf: is{p.Name}Of");
                                sw.WriteLine($"ObjectProperty: is{p.Name}Of");
                            }
                        }
                    }
                }

                name = UniqueIndividualName(name);
                individuals.Add(o, [name]);

                // XXX Importante isso aqui por causa da recurs�o!
                List<(string, string)> postpone = [];

                foreach (var p in o.GetType().FindMembers(MemberTypes.Field, BindingFlags.Instance | BindingFlags.NonPublic, (m, fc) => true, null))
                {
                    if (p is FieldInfo fi)
                    {
                        // XXX Ontologias n�o tem listas
                        //     Ent�o repetimos v�rias vezes a mesma rela��o!
                        var valor = fi.GetValue(o);
                        if (valor!.GetType().IsAssignableTo(typeof(IEnumerable)))
                        {
                            foreach (object obj in (IEnumerable)valor)
                                postpone.Add(("has" + fi.Name, Traverse(obj, obj.GetType().Name)));
                        }
                    }
                }

                sw.WriteLine($"Individual: {name}");
                sw.WriteLine($"  Types: {o.GetType().Name}");
                foreach ((string relation, string indi) in postpone)
                    sw.WriteLine($"  Facts: {relation} {indi}");

                return name;
            }
        }

        string UniqueIndividualName(string name)
        {
            while (allIndividualNames.Contains(name))
            {
                name += " - (Copia)"; // Toma Windows venceu.
            }

            return name;
        }
    }
}

internal abstract class Topping { }

internal abstract class ToppingVeggie : Topping { }

internal abstract class ToppingCheese : Topping { }

internal class Mushroom : ToppingVeggie { }

internal class Tomato : ToppingVeggie { }

internal class Mozzarella : ToppingCheese { }

internal abstract class Pizza
{
    internal Topping[] toppings;
}

internal abstract class PizzaVeggie : Pizza { }

internal class Margherita : PizzaVeggie
{
    internal Margherita()
    {
        toppings = [new Mozzarella(), new Tomato()];
    }
}

internal class PizzaMushroom : Pizza
{
    internal PizzaMushroom()
    {
        toppings = [new Mozzarella(), new Mushroom()];
    }
}
