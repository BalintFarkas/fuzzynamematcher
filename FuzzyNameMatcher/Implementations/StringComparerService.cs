using FuzzyNameMatcher;
using FuzzyNameMatcher.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace FuzzyNameMatcher.Implementations
{
    public class StringComparerService : IStringComparerService
    {
        #region Constants
        /// <summary>
        /// A token's weight that is found only through some edit distance is to be raised to a power of this exponent.
        /// </summary>
        const double EditDistance_DecayExponentBase = 0.85;

        /// <summary>
        /// The maximum edit distance that is allowed, in absolute value.
        /// (Has a large role in performance, since this is taken into account before running
        /// the costly distance calculation itself.)
        /// </summary>
        const double EditDistance_ThresholdMax = 5;
        #endregion

        #region Main methods
        public double GetSimilarityScore(string input, List<string> targets)
        {
            return targets.Select(target => GetSimilarityScore(input, target)).Max();
        }

        /// <summary>
        /// Returns the similarity score for the two input values, normalized between 1 and 0. 1 is perfect similarity.
        /// </summary>
        public double GetSimilarityScore(string input, string target)
        {
            //Defend against nulls amd empty strings
            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(target)) return 0;

            //Deal with the trivial case quickly
            if (input == target) return 1;

            //Purge input strings
            input = Purge(input);
            target = Purge(target);

            //Tokenize input strings
            var inputTokens = Tokenize(input);
            var candidateTokens = Tokenize(target);

            //Do matching
            double similarityScore = MatchTokens(inputTokens, candidateTokens);

            //Return calculated similarity score
            return similarityScore;
        }
        #endregion

        #region Purging
        /// <summary>
        /// Canonizes the string for easier matching by discarding unnecessary information. 
        /// Makes it lowercase, removes diacritics, removes non-alphanumeric characters and retains only single spaces.
        /// </summary>
        private string Purge(string input)
        {
            //To lower case
            input = input.ToLower();

            //Remove diacritics (á => a, etc.)
            input = input.Normalize(NormalizationForm.FormD);

            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                UnicodeCategory unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(input[i]);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(input[i]);
                }
            }

            input = stringBuilder.ToString();

            //Retain only alphanumeric characters and single spaces; string may not begin with a space
            stringBuilder = new StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                if (char.IsLetterOrDigit(input[i]) || //Alphanumeric character
                    (input[i] == ' ' && stringBuilder.Length > 0 && stringBuilder[stringBuilder.Length - 1] != ' ')) //This is a space, it is not the first character, and the previous character has not been a space
                {
                    stringBuilder.Append(input[i]);
                }
            }

            input = stringBuilder.ToString();

            //Return result
            return input;
        }
        #endregion

        #region Tokenization
        /// <summary>
        /// Tokenizes the string by splitting it up at whitespaces and assigning weights to the tokens.
        /// </summary>
        private Token[] Tokenize(string input)
        {
            //Split the string
            var rawTokens = input.Split(' ');

            //Encapsulate strings into token objects and assign default weights in the process
            List<Token> weightedTokens = new List<Token>();
            foreach (var stringToken in rawTokens)
            {
                weightedTokens.Add(new Token()
                {
                    Value = stringToken,
                    Weight = GetDefaultTokenWeight(stringToken),
                    AwardedWeight = 0
                });
            }

            //Return result
            return weightedTokens.ToArray();
        }

        /// <summary>
        /// Returns default token weight for a given string, based solely on its value.
        /// Currently always returns 1; if some common words are unimportant, their starting weight can be
        /// reduced here.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private double GetDefaultTokenWeight(string value)
        {
            return 1;
        }
        #endregion

        #region Matching
        /// <summary>
        /// Performs token matching.
        /// </summary>
        private double MatchTokens(Token[] inputTokens, Token[] candidateTokens)
        {
            //Find matches for tokens
            foreach (Token inputToken in inputTokens)
            {
                //Trivial case - let's see if the token is contained 1:1
                if (candidateTokens.Select(i => i.Value).Contains(inputToken.Value))
                {
                    inputToken.AwardedWeight = inputToken.Weight;
                }
                //Try to find an approximate match.
                //If the best approximate match is above a threshold, we award partial weight for it.
                else
                {
                    //Do pre-optimalization: the edit distance cannot be smaller than the difference
                    //between string lengths
                    var culledCandidates = candidateTokens.Where(i => Math.Abs(inputToken.Value.Length - i.Value.Length) <= EditDistance_ThresholdMax);

                    //If anything remains, do the (expensive) distance calculations
                    if (culledCandidates.Count() > 0)
                    {
                        int minimumDistance = culledCandidates.Min(i => GetEditDistance(inputToken.Value, i.Value));

                        if (minimumDistance <= EditDistance_ThresholdMax)
                        {
                            inputToken.AwardedWeight = inputToken.Weight * Math.Pow(EditDistance_DecayExponentBase, minimumDistance);
                        }
                    }
                }
            }

            // Get root mean square
            // By taking the square of weights, fractional weights (partial matches) are "penalized"
            // compared to full matches (with value of 1), which is desirable. This way, a 2-part name
            // where one part of the name is a full match and the other is a partial match gets
            // a lot higher score than a name where both parts are just partial matches.
            return GetRootMeanSquare(inputTokens.Select(t => t.AwardedWeight).ToList());
        }

        private double GetRootMeanSquare(List<double> weights)
        {
            if (weights.Count == 0) return 0;
            return Math.Sqrt(weights.Select(w => Math.Pow(w, 2)).Sum() / weights.Count);
        }

        /// <summary>
        /// Computes the Damerau-Levenshtein distance between two strings.
        /// </summary>
        public int GetEditDistance(string source, string target)
        {
            if (String.IsNullOrEmpty(source))
            {
                if (String.IsNullOrEmpty(target))
                {
                    return 0;
                }
                else
                {
                    return target.Length;
                }
            }
            else if (String.IsNullOrEmpty(target))
            {
                return source.Length;
            }

            Int32 m = source.Length;
            Int32 n = target.Length;
            Int32[,] H = new Int32[m + 2, n + 2];

            Int32 INF = m + n;
            H[0, 0] = INF;
            for (Int32 i = 0; i <= m; i++) { H[i + 1, 1] = i; H[i + 1, 0] = INF; }
            for (Int32 j = 0; j <= n; j++) { H[1, j + 1] = j; H[0, j + 1] = INF; }

            SortedDictionary<Char, Int32> sd = new SortedDictionary<Char, Int32>();
            foreach (Char Letter in (source + target))
            {
                if (!sd.ContainsKey(Letter))
                    sd.Add(Letter, 0);
            }

            for (Int32 i = 1; i <= m; i++)
            {
                Int32 DB = 0;
                for (Int32 j = 1; j <= n; j++)
                {
                    Int32 i1 = sd[target[j - 1]];
                    Int32 j1 = DB;

                    if (source[i - 1] == target[j - 1])
                    {
                        H[i + 1, j + 1] = H[i, j];
                        DB = j;
                    }
                    else
                    {
                        H[i + 1, j + 1] = Math.Min(H[i, j], Math.Min(H[i + 1, j], H[i, j + 1])) + 1;
                    }

                    H[i + 1, j + 1] = Math.Min(H[i + 1, j + 1], H[i1, j1] + (i - i1 - 1) + 1 + (j - j1 - 1));
                }

                sd[source[i - 1]] = i;
            }

            return H[m + 1, n + 1];
        }
        #endregion

        #region Token class
        /// <summary>
        /// The custom Token data structure.
        /// </summary>
        private class Token
        {
            /// <summary>
            /// The token's string value.
            /// </summary>
            public string Value { get; set; }

            /// <summary>
            /// The token's weight.
            /// </summary>
            public double Weight { get; set; }

            /// <summary>
            /// Holds how much weight was actually awarded for this token at matching.
            /// Should be equal to Weight if the token is found in its entirety, 
            /// and appropriately reduced, if it is found only partially.
            /// </summary>
            public double AwardedWeight { get; set; }
        }
        #endregion
    }
}
