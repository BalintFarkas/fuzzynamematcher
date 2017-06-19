using System;
using System.Collections.Generic;
using System.Text;

namespace FuzzyNameMatcher.Interfaces
{
    public interface IStringComparerService
    {
        double GetSimilarityScore(string input, List<string> targets);
        double GetSimilarityScore(string input, string target);
    }
}
