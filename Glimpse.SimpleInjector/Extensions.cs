using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Glimpse.SimpleInjector
{
    public static class Extensions
    {
        public static string AsFriendlyName(this Type type)
        {
            if (type.IsGenericType)
            {
                return string.Format(
                    "{0}<{1}>",
                    type.Name.Split('`')[0],
                    string.Join(",", type.GetGenericArguments().Select(x => AsFriendlyName(x))));
            }
            else
            {
                return type.Name;
            }
        }
    }
}
