namespace Chatbot.API.Core.Data
{
    public static class SeedUrls
    {
        public static readonly List<string> Urls = new List<string>
        {
            "https://www.ubagroup.com/about-us/who-we-are/",
            "https://www.ubagroup.com/about-us/our-history/",
            "https://www.ubagroup.com/about-us/our-strategy/",
            "https://www.ubagroup.com/about-us/ceos-overview/",
            "https://www.ubagroup.com/about-us/leadership/",
            "https://www.ubagroup.com/about-us/awards-and-achievements/",
            "https://www.ubagroup.com/quick-facts/",
            "https://www.ubagroup.com/global-presence/",
            "https://www.ubagroup.com/help/frequently-asked-questions/",
            "https://www.ubagroup.com/help/contact-us/",
            "https://www.ubagroup.com/help/self-service/",
            "https://www.ubagroup.com/help/security-centre/",
            "https://www.ubagroup.com/global-bvn-registration/",
            "https://www.ubagroup.com/about-us/careers/",
            "https://www.ubagroup.com/about-us/careers/working-at-uba/",
            "https://www.ubagroup.com/our-impact/",
            "https://www.ubagroup.com/our-impact/sustainability/",
            "https://www.ubagroup.com/our-impact/uba-foundation/education/",
            "https://www.ubagroup.com/our-impact/uba-foundation/empowerment/",
            "https://www.ubagroup.com/our-impact/uba-foundation/environment/",
            "https://www.ubagroup.com/whistleblowing/",
            "https://www.ubagroup.com/media-centre/news/",
            "https://www.ubagroup.com/media-centre/press-releases/"
        };


        public static string GetCategoryFromUrl(string url)
        {
            if (url.Contains("/about-us/")) return "about";
            if (url.Contains("/help/")) return "help";
            if (url.Contains("/careers/")) return "careers";
            if (url.Contains("/our-impact/")) return "impact";
            if (url.Contains("/media-centre/")) return "media";
            if (url.Contains("/whistleblowing/")) return "governance";
            if (url.Contains("/global-bvn-registration/")) return "help";
            if (url.Contains("/quick-facts/")) return "about";
            if (url.Contains("/global-presence/")) return "about";
            return "general";
        }
    }
}