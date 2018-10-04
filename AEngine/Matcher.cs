using SourceAFIS.Simple;
using System;

namespace AEngine
{
    public class Matcher
    {
        private  AfisEngine Afis = new AfisEngine();

        public float Match(string query, string template)
        {
            Fingerprint fp1 = new Fingerprint { Template = Convert.FromBase64String(query) };
            Fingerprint fp2 = new Fingerprint { Template = Convert.FromBase64String(template) };

            Person person1 = new Person();
            person1.Fingerprints.Add(fp1);
            Person person2 = new Person();
            person2.Fingerprints.Add(fp2);

            Afis.Threshold = 30;
            var res2 = Afis.Verify(person1, person2);
            return res2;
        }
    }
}
