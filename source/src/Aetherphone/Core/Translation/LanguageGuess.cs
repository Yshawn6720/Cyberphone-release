namespace Aetherphone.Core.Translation;

internal static class LanguageGuess
{
    public const string Undetermined = "und";

    private const int MinimumWords = 3;
    private const int MinimumScore = 2;

    private static readonly string[] Codes = { "en", "fr", "de", "tr", "es", "pt" };

    private static readonly HashSet<string>[] FunctionWords =
    {
        new(StringComparer.Ordinal)
        {
            "the", "and", "is", "are", "was", "were", "to", "of", "in", "that", "it", "for", "with", "this", "on",
            "you", "have", "has", "had", "be", "been", "not", "but", "my", "we", "they", "at", "from", "so", "just",
            "what", "about", "all", "can", "will", "your", "me", "if", "anyone", "someone", "there", "here", "when",
            "who", "how", "i", "i'm", "i'll", "i've", "don't", "can't", "won't", "didn't", "doesn't", "isn't", "it's",
            "that's", "he", "she", "him", "her", "his", "our", "us", "them", "their", "do", "does", "did", "get",
            "got", "go", "going", "went", "like", "really", "very", "much", "more", "some", "any", "or", "because",
            "then", "than", "now", "today", "tonight", "tomorrow", "yesterday", "still", "also", "only", "would",
            "could", "should", "want", "need", "know", "think", "thanks", "please", "again", "never", "always",
            "out", "up", "down", "over", "into", "back", "one", "first", "new", "good", "great", "well", "too",
            "guys", "friend", "friends", "last", "night", "evening", "morning", "week", "looking", "let's",
            "hi", "hey", "yo", "lol", "lmao", "bro", "dude", "man", "boy", "girl", "nice", "cute", "love", "omg", "wow",
            "cool", "thx", "ty", "pls", "handsome", "beautiful", "hello", "bye", "ok", "okay", "yeah", "yes", "haha",
        },
        new(StringComparer.Ordinal)
        {
            "le", "la", "les", "l'", "et", "est", "sont", "des", "du", "un", "une", "que", "qui", "quoi", "pas",
            "ne", "n'", "pour", "dans", "avec", "sur", "sous", "ce", "cet", "cette", "ces", "c'", "je", "j'", "tu",
            "il", "elle", "nous", "vous", "ils", "elles", "mais", "ou", "où", "au", "aux", "très", "plus", "moins",
            "mon", "ma", "mes", "ton", "ta", "tes", "son", "sa", "ses", "notre", "votre", "leur", "leurs", "moi",
            "toi", "lui", "eux", "m'", "t'", "s'", "y", "quelqu'un", "c'est", "j'ai", "être", "avoir", "était",
            "été", "fait", "faire", "va", "vais", "aller", "peut", "veux", "veut", "beaucoup", "aujourd'hui",
            "demain", "hier", "soir", "toujours", "jamais", "encore", "aussi", "bien", "bon", "bonne", "merci",
            "bonjour", "salut", "oui", "non", "quand", "comme", "tout", "tous", "toute", "toutes", "rien",
            "quelque", "chose", "ici", "là", "déjà", "alors", "donc", "parce", "car", "cherche", "cherchons",
            "ami", "amie", "amis", "qu'", "d'", "chez", "vers", "depuis", "pendant", "sans", "entre",
            "coucou", "bisous", "mec", "meuf", "trop", "ouais", "mdr", "ptdr", "bonsoir", "nuit", "ça", "voilà",
            "mignon", "mignonne", "génial",
        },
        new(StringComparer.Ordinal)
        {
            "der", "die", "das", "den", "dem", "des", "ein", "eine", "einen", "einem", "einer", "und", "ist",
            "sind", "war", "waren", "ich", "du", "er", "sie", "es", "wir", "ihr", "mich", "dich", "mir", "dir",
            "uns", "euch", "ihn", "ihm", "nicht", "kein", "keine", "zu", "mit", "auf", "für", "von", "aus", "bei",
            "nach", "vor", "über", "unter", "um", "auch", "aber", "wenn", "noch", "nur", "schon", "sehr", "oder",
            "bin", "bist", "hat", "habe", "haben", "hatte", "wird", "werden", "kann", "muss", "will", "möchte",
            "mein", "meine", "dein", "deine", "sein", "seine", "ihre", "unser", "euer", "heute", "morgen",
            "gestern", "abend", "jetzt", "immer", "nie", "wieder", "hier", "da", "dort", "dann", "also", "denn",
            "weil", "dass", "ob", "wie", "was", "wer", "wo", "ja", "nein", "gut", "danke", "bitte", "hallo",
            "jemand", "suche", "suchen", "gruppe", "freund", "freundin", "freunde", "gelangweilt",
            "moin", "servus", "digga", "geil", "krass", "süß", "hübsch", "tschüss", "alter", "bruder", "schatz",
            "liebe", "schön", "toll",
        },
        new(StringComparer.Ordinal)
        {
            "ve", "bir", "bu", "şu", "için", "ile", "çok", "ama", "fakat", "ben", "sen", "biz", "siz", "onlar",
            "bana", "sana", "ona", "bize", "size", "beni", "seni", "onu", "benim", "senin", "onun", "bizim",
            "sizin", "ne", "var", "yok", "gibi", "daha", "mi", "mı", "mu", "mü", "değil", "ki", "her", "kadar",
            "sonra", "önce", "ise", "ya", "hem", "evet", "hayır", "iyi", "kötü", "olan", "olarak", "bugün",
            "yarın", "dün", "akşam", "şimdi", "hep", "hiç", "yine", "burada", "orada", "çünkü", "nasıl",
            "neden", "kim", "nerede", "teşekkürler", "merhaba", "selam", "lütfen", "arıyorum", "arıyoruz",
            "kimse", "biri", "arkadaşım", "arkadaş", "gece", "bravo", "çevirdiniz", "yazıyı",
            "canım", "abi", "abla", "kanka", "aşkım", "hoş", "güzel", "tamam", "sağol", "eyvallah", "hadi",
            "kardeşim", "naber", "iyiyim", "nasılsın", "günaydın", "geceler",
        },
        new(StringComparer.Ordinal)
        {
            "el", "los", "las", "un", "una", "unos", "unas", "y", "es", "son", "era", "está", "están", "estoy",
            "fue", "ser", "estar", "del", "al", "en", "que", "qué", "quién", "no", "sí", "por", "para", "con",
            "sin", "se", "su", "sus", "lo", "le", "les", "nos", "mi", "mis", "tu", "tus", "yo", "tú", "él", "ella",
            "nosotros", "ellos", "ellas", "pero", "más", "menos", "como", "muy", "mucho", "este", "esta", "esto",
            "ese", "esa", "eso", "hay", "hoy", "mañana", "ayer", "ahora", "siempre", "nunca", "también", "todo",
            "todos", "algo", "alguien", "nada", "nadie", "cuando", "donde", "porque", "entonces", "ya", "aún",
            "bien", "gracias", "hola", "busco", "buscamos", "tiene", "tengo", "amigo", "amiga", "amigos",
            "noche", "aburrido", "aburría",
            "guapo", "guapa", "amor", "vale", "jaja", "jajaja", "tío", "tía", "buenas", "buenos", "días", "noches",
            "cariño", "lindo", "linda",
        },
        new(StringComparer.Ordinal)
        {
            "o", "os", "as", "um", "uma", "uns", "umas", "e", "é", "são", "era", "está", "estão", "estou",
            "foi", "ser", "estar", "do", "da", "dos", "das", "no", "na", "nos", "nas", "em", "que", "quem", "não",
            "sim", "por", "para", "com", "sem", "se", "seu", "sua", "seus", "suas", "lhe", "meu", "minha",
            "meus", "minhas", "teu", "tua", "eu", "você", "vocês", "ele", "ela", "nós", "eles", "elas", "mas",
            "mais", "menos", "como", "muito", "este", "esta", "isto", "esse", "essa", "isso", "aquele", "há",
            "hoje", "amanhã", "ontem", "agora", "sempre", "nunca", "também", "tudo", "todos", "algo", "alguém",
            "nada", "ninguém", "quando", "onde", "porque", "então", "já", "ainda", "bem", "obrigado", "obrigada",
            "olá", "oi", "procuro", "procuramos", "tem", "tenho", "amigo", "amiga", "amigos", "noite", "pela",
            "pelo", "entediado",
            "kkk", "kkkk", "mano", "cara", "beleza", "valeu", "bom", "boa", "dia", "tarde", "querido", "querida",
            "gato", "gata", "fofo", "fofa", "amor", "lindo", "linda",
        },
    };

    public static string Detect(string text)
    {
        var kana = 0;
        var han = 0;
        var hangul = 0;
        var cyrillic = 0;
        var latin = 0;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (IsKana(character))
            {
                kana++;
            }
            else if (IsHan(character))
            {
                han++;
            }
            else if (character is >= '가' and <= '힯')
            {
                hangul++;
            }
            else if (character is >= 'Ѐ' and <= 'ӿ')
            {
                cyrillic++;
            }
            else if (char.IsLetter(character))
            {
                latin++;
            }
        }

        if (kana >= 2)
        {
            return "ja";
        }

        if (han >= 2)
        {
            return "zh";
        }

        if (hangul >= 2)
        {
            return "ko";
        }

        if (cyrillic >= 4 && cyrillic > latin)
        {
            return "ru";
        }

        return latin == 0 ? Undetermined : ScoreLatin(text);
    }

    private static string ScoreLatin(string text)
    {
        Span<int> scores = stackalloc int[Codes.Length];
        var words = 0;
        var start = -1;
        var lowered = text.ToLowerInvariant().Replace('’', '\'');
        for (var index = 0; index <= lowered.Length; index++)
        {
            var inWord = index < lowered.Length
                && (char.IsLetter(lowered[index]) || char.IsDigit(lowered[index]) || lowered[index] is '\'' or '@' or '#' or ':' or '/' or '.');
            if (inWord)
            {
                if (start < 0)
                {
                    start = index;
                }

                continue;
            }

            if (start < 0)
            {
                continue;
            }

            var word = lowered.Substring(start, index - start);
            start = -1;
            if (word[0] is '@' or '#' || word.Contains("://", StringComparison.Ordinal))
            {
                continue;
            }

            word = word.Trim('.', ':', '/');
            if (word.Length == 0)
            {
                continue;
            }

            words++;
            ScoreWord(word, scores);
            var apostrophe = word.IndexOf('\'');
            if (apostrophe > 0 && apostrophe < word.Length - 1)
            {
                ScoreWord(word[..(apostrophe + 1)], scores);
                ScoreWord(word[(apostrophe + 1)..], scores);
            }
        }

        for (var index = 0; index < lowered.Length; index++)
        {
            switch (lowered[index])
            {
                case 'ı':
                case 'ğ':
                case 'ş':
                    scores[3] += 2;
                    break;
                case 'ã':
                case 'õ':
                    scores[5] += 2;
                    break;
                case 'ñ':
                case '¿':
                case '¡':
                    scores[4] += 2;
                    break;
                case 'ß':
                    scores[2] += 2;
                    break;
                case 'œ':
                case 'è':
                case 'ê':
                case 'ù':
                case 'î':
                case 'û':
                    scores[1] += 1;
                    break;
            }
        }

        var best = 0;
        var bestScore = -1;
        var secondScore = -1;
        for (var language = 0; language < Codes.Length; language++)
        {
            if (scores[language] > bestScore)
            {
                secondScore = bestScore;
                bestScore = scores[language];
                best = language;
            }
            else if (scores[language] > secondScore)
            {
                secondScore = scores[language];
            }
        }

        if (bestScore < MinimumScore || bestScore <= secondScore)
        {
            return Undetermined;
        }

        return words >= MinimumWords || secondScore <= 0 ? Codes[best] : Undetermined;
    }

    private static void ScoreWord(string word, Span<int> scores)
    {
        for (var language = 0; language < Codes.Length; language++)
        {
            if (FunctionWords[language].Contains(word))
            {
                scores[language]++;
            }
        }
    }

    private static bool IsKana(char character) =>
        character is (>= '぀' and <= 'ヿ') or (>= 'ｦ' and <= 'ﾟ');

    private static bool IsHan(char character) =>
        character is (>= '一' and <= '鿿') or (>= '㐀' and <= '䶿');
}
