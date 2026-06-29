namespace Xipha.Crawl;

public static class SearchTerms
{
    /// Latin prefixes — covers English and brand drug names
    public static readonly IReadOnlyList<string> Latin =
    [
        "ac","al","am","ap","ar","as","at","az",
        "be","bi","bo","bu",
        "ca","ce","ch","ci","cl","co",
        "da","de","di","do","du",
        "em","en","er","es","et","ez",
        "fa","fe","fi","fl","fo","fu",
        "ga","ge","gl",
        "he","hy",
        "ib","im","in","ip","ir","is",
        "ke",
        "la","le","li","lo",
        "me","mi","mo",
        "na","ni","no","ny",
        "of","ol","om","on","os","ox",
        "pa","pe","ph","pi","pr","py",
        "qu",
        "ra","ri","ro",
        "sa","se","si","so","sp","su",
        "ta","te","th","ti","to","tr",
        "va","ve","vi",
        "wa","zo"
    ];

    /// Persian prefixes — covers Persian drug names
    public static readonly IReadOnlyList<string> Persian =
    [
        "آ","اب","اد","ار","از","اس","اف","اک","ال","ام","ان","اه","ای",
        "با","بت","بر","به",
        "پر","پن","پو","پی",
        "تر","تن","تو",
        "دا","دک","دو","دی",
        "ری","رو",
        "سا","سد","سر","سف","سل","سم","سن","سو","سی",
        "فر","فن",
        "کا","کپ","کر","کل","کم","کن","کو","کی",
        "لا","لو",
        "ما","مت","مد","مر","مف","مل","من","مو","می",
        "نا","نر","نو","نی",
        "وا","هی","یو"
    ];

    /// Both sets combined (~230 terms)
    public static IEnumerable<string> All => Latin.Concat(Persian);
}