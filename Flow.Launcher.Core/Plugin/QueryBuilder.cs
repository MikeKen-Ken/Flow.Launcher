using System;
using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Plugin
{
    public static class QueryBuilder
    {
        public static Query Build(string originalQuery, string trimmedQuery, Dictionary<string, List<PluginPair>> nonGlobalPlugins)
        {
            // home query
            if (string.IsNullOrEmpty(trimmedQuery))
            {
                return new Query()
                {
                    Search = string.Empty,
                    OriginalQuery = string.Empty,
                    TrimmedQuery = string.Empty,
                    SearchTerms = Array.Empty<string>(),
                    ActionKeyword = string.Empty,
                    IsHomeQuery = true
                };
            }

            // replace multiple white spaces with one white space
            var terms = trimmedQuery.Split(Query.TermSeparator, StringSplitOptions.RemoveEmptyEntries);
            if (terms.Length == 0)
            {
                // nothing was typed
                return null;
            }

            string actionKeyword, search;
            string possibleActionKeyword = terms[0];
            string[] searchTerms;

            if (TryGetActionKeyword(nonGlobalPlugins, possibleActionKeyword, out actionKeyword))
            {
                // use non global plugin for query
                // Slice by the typed token. Case-insensitive matches have the same length.
                search = terms.Length > 1 ? trimmedQuery[(possibleActionKeyword.Length + 1)..].TrimStart() : string.Empty;
                searchTerms = terms[1..];
            }
            else
            {
                // non action keyword
                actionKeyword = string.Empty;
                search = trimmedQuery.TrimStart();
                searchTerms = terms;
            }

            return new Query()
            {
                Search = search,
                OriginalQuery = originalQuery,
                TrimmedQuery = trimmedQuery,
                SearchTerms = searchTerms,
                ActionKeyword = actionKeyword,
                IsHomeQuery = false
            };
        }

        /// <summary>
        /// Matches an action keyword without requiring the same letter case.
        /// The returned keyword is the registered spelling, so later exact comparisons still succeed.
        /// An exact registered keyword that belongs only to disabled plugins stays unmatched.
        /// </summary>
        private static bool TryGetActionKeyword(
            Dictionary<string, List<PluginPair>> nonGlobalPlugins,
            string possibleActionKeyword,
            out string actionKeyword)
        {
            if (nonGlobalPlugins.TryGetValue(possibleActionKeyword, out var exactPairs))
            {
                if (HasAnyEnabledPlugin(exactPairs))
                {
                    actionKeyword = possibleActionKeyword;
                    return true;
                }

                actionKeyword = string.Empty;
                return false;
            }

            foreach (var pair in nonGlobalPlugins)
            {
                if (!string.Equals(pair.Key, possibleActionKeyword, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!HasAnyEnabledPlugin(pair.Value))
                {
                    continue;
                }

                actionKeyword = pair.Key;
                return true;
            }

            actionKeyword = string.Empty;
            return false;
        }

        private static bool HasAnyEnabledPlugin(List<PluginPair> pluginPairs)
        {
            lock (pluginPairs)
            {
                return pluginPairs.Any(plugin => !plugin.Metadata.Disabled);
            }
        }
    }
}
