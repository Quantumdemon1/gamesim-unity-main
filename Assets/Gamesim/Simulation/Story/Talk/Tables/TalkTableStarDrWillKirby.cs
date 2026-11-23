namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, one All-Star template's own romance talks (slice S3, talk rules 3; ruling R1: the talks are C# tables). The
    /// talks builder fills <see cref="Rows"/>: rows of the romance kinds, Owner = <see cref="Owner"/>, each kind the twelve
    /// All-Star tables all hold. Game register only: the house, the competitions, juries, the season and their name as a player.
    /// Tame. American spelling.
    /// </summary>
    public static class TalkTableStarDrWillKirby
    {
        public const string Owner = "dr-will-kirby";

        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "will.rom.flirt.1", Owner = Owner, Kind = TalkKind.FlirtLine,
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"You're my favorite person to talk game with.\"",
                        Lands = "\"Of course I am. I'm everyone's. But you get the good version.\"",
                        Even = "\"Naturally.\"",
                        Grates = "\"Flattery. My own move. Bold.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "will.rom.flirt.2", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 15 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'd pick you for every comp team.\"",
                        Lands = "\"Smart. I'd carry the team by not competing at all.\"",
                        Even = "\"Charming.\"",
                        Grates = "\"Pick someone who wins comps, then.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "will.rom.flirt.3", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 30 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I keep finding reasons to sit by you.\"",
                        Lands = "\"I noticed. I approve of the reasons.\"",
                        Even = "\"Hm.\"",
                        Grates = "\"Sit anywhere. I'm always the best company.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "will.rom.open.1", Owner = Owner, Kind = TalkKind.Opening,
                Open = "(feet up on the coffee table) \"You know I lie in here for sport, right? So when I say I enjoy you, you'll have to guess.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'll guess you mean it.\"",
                        Lands = "\"Brave. And correct. Don't tell anyone.\"",
                        Even = "\"Maybe. Keep guessing.\"",
                        Grates = "\"Oh, you're easy.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I'll guess you're lying.\"",
                        Lands = "\"Smart. Usually. Not this time.\"",
                        Even = "\"Even odds. Like everything.\"",
                        Grates = "\"Wrong. Pity.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"I'll wait and see.\"",
                        Lands = "\"The wisest answer in the house.\"",
                        Even = "\"Fine. Watch closely.\"",
                        Grates = "\"Waiting is boring.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. How refreshing. I'll lie to you slightly less.\"" },
                },
            },
            new TalkRow
            {
                Id = "will.rom.open.2", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 15 },
                Open = "(sliding you the last coffee) \"I don't share the good coffee. Ask anyone. Consider yourself a rare exception.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I like being your exception.\"",
                        Lands = "\"Don't let it go to your head. It went to mine.\"",
                        Even = "\"Enjoy it while it lasts.\"",
                        Grates = "\"Now you've made it awkward.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Thank you. Really.\"",
                        Lands = "\"You're welcome. Sincerely. Rare for me.\"",
                        Even = "\"Drink it before I change my mind.\"",
                        Grates = "\"Gratitude. How quaint.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"What's it going to cost me?\"",
                        Lands = "\"A vote, eventually. And your company now.\"",
                        Even = "\"We'll negotiate later.\"",
                        Grates = "\"Ruin a nice moment, why don't you.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. How refreshing. I'll lie to you slightly less.\"" },
                },
            },
            new TalkRow
            {
                Id = "will.rom.open.3", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 30 },
                Open = "(lowering his voice for once) \"Don't repeat this. I'd keep you over almost anyone in this house. That's strategy. Mostly.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Mostly?\"",
                        Lands = "\"Mostly. The rest I'm keeping to myself.\"",
                        Even = "\"Mostly. Don't push it.\"",
                        Grates = "\"Fine. Entirely strategy.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'd keep you too.\"",
                        Lands = "\"Then we're a pair. A dangerous one.\"",
                        Even = "\"Good. Noted.\"",
                        Grates = "\"Don't be sentimental.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Strategy I can trust.\"",
                        Lands = "\"Exactly. Sentiment is for the jury speech.\"",
                        Even = "\"Smart.\"",
                        Grates = "\"Trust is a strong word.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. How refreshing. I'll lie to you slightly less.\"" },
                },
            },
            new TalkRow
            {
                Id = "will.rom.hold.1", Owner = Owner, Kind = TalkKind.HoldBack,
                Open = "(a slow, thoughtful look) \"Careful. I'm not saying no. I'm saying let's see where this game takes us.\"",
            },
            new TalkRow
            {
                Id = "will.rom.turn.1", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 1,
                Open = "(grinning, delighted) \"Oh, that's very kind. The nicest thing anyone has said to me since the last nomination ceremony.\"",
                Second = "\"But let me save you some time. I'm here to play. Purely. Shamelessly. Want something from me? Ask for a vote. Much easier.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.LaughItOff, Line = "\"Fair enough. Can't blame me for trying.\"", Lands = "\"A vote costs a secret. Yours, not mine. Sit down, let's talk numbers.\"" },
                    new TalkReply { Choice = TalkChoice.PushAgain, Line = "\"Come on. Nobody's only here to play.\"", Lands = "\"I am. And now you're the one who couldn't take a no from the most charming player in the house. Bad look.\"" },
                    new TalkReply { Choice = TalkChoice.Apologize, Line = "\"Sorry. I read that wrong.\"", Lands = "\"You read it fine. You just picked the wrong book. No harm done. Now, about this week.\"" },
                },
            },
            new TalkRow
            {
                Id = "will.rom.turn.2", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 2,
                Open = "(not even looking up from the cards) \"Same answer as last time. Still here to play. Still charming about it.\"",
            },
            new TalkRow
            {
                Id = "will.rom.turn.3", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 3,
                Open = "(warm, final) \"Let's retire this bit. It was funny twice. I'd rather beat you at this game than let you charm me.\"",
            },
            new TalkRow
            {
                Id = "will.rom.softno.1", Owner = Owner, Kind = TalkKind.SoftNo,
                Open = "(kindly, for once without a joke) \"I like you. You're my favorite person to scheme with. That's where I leave it.\"",
            },
            new TalkRow
            {
                Id = "will.rom.declare.1", Owner = Owner, Kind = TalkKind.Declaration,
                Open = "(no audience, no grin) \"Here's a rare one: no lie. I want us to play this whole thing as a pair. You and me. Final two.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.Accept, Line = "\"No lie back. Yes.\"", Lands = "\"Wonderful. The house will hate it. Wonderful.\"" },
                    new TalkReply { Choice = TalkChoice.NotYet, Line = "\"Not yet. Ask me again.\"", Lands = "\"I will. I'm very persistent and very charming.\"" },
                    new TalkReply { Choice = TalkChoice.NotLikeThat, Line = "\"I don't feel it like that.\"", Lands = "\"Fair. Rejection, how novel. We stay allies.\"" },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends it is. My only honest alliance.\"" },
                },
            },
            new TalkRow
            {
                Id = "will.rom.yes.1", Owner = Owner, Kind = TalkKind.Accepts,
                Open = "(theatrical, then not) \"Yes. Obviously. I was hoping you'd be brave enough to ask first.\"",
            },
            new TalkRow
            {
                Id = "will.rom.no.1", Owner = Owner, Kind = TalkKind.Refuses,
                Open = "(gently) \"No. I'd hate to be the move that costs you the game. Allies. Good ones.\"",
            },
            new TalkRow
            {
                Id = "will.rom.couple.1", Owner = Owner, Kind = TalkKind.Couple,
                Open = "(on the yard couch) \"Everyone thinks I'm playing you. Let them. Being underestimated as a pair is wonderful.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Let them think it.\"",
                        Lands = "\"Exactly. We'll explain it at the finale.\"",
                        Even = "\"Always.\"",
                        Grates = "\"Don't be dull about it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Let's keep it low-key this week.\"",
                        Lands = "\"Low-key. I can do low-key. Very nearly.\"",
                        Even = "\"Fine.\"",
                        Grates = "\"Low-key? Have we met?\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"I think we should go back to being allies.\"", Lands = "\"Allies, then. Gracefully done. I'll still lie for you at the vote.\"" },
                },
            },
            new TalkRow
            {
                Id = "will.rom.ex.1", Owner = Owner, Kind = TalkKind.Ex,
                Open = "(airily) \"We're fine. Better than fine. I'll need a week to come up with a new nickname for you.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I hope we're okay.\"",
                        Lands = "\"We're splendid. Truly.\"",
                        Even = "\"We will be.\"",
                        Grates = "\"Not now.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take all the time you need.\"",
                        Lands = "\"Generous. I'll take two days.\"",
                        Even = "\"Fine.\"",
                        Grates = "\"I need none.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I still want us to be friends.\"",
                        Lands = "\"Allies. My favorite word.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Let's see.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "will.rom.jealous.1", Owner = Owner, Kind = TalkKind.Jealousy,
                Open = "(arms folded, smiling thinly) \"I hear you've been charming someone else. That's my move. Explain yourself.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It wasn't what it looked like.\"",
                        Lands = "\"Of course not. Nothing ever is.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"It looked exactly like that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're the one I want. I mean it.\"",
                        Lands = "\"Fine words. I'll consider them.\"",
                        Even = "\"We'll see.\"",
                        Grates = "\"I say that too. I lie too.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're listening to gossip.\"",
                        Lands = "\"Gossip is my native language.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Don't dodge.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"Maybe we should end this. Kindly.\"", Lands = "\"Gracefully done. Allies, then. I'll still lie for you at the vote.\"" },
                },
            },
            new TalkRow
            {
                Id = "will.rom.react1.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 1,
                Open = "(from the couch, not looking up) \"You two aren't subtle. I say this as the least subtle person in the house.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Subtle was never the plan.\"",
                        Lands = "\"Clearly. It's working against you.\"",
                        Even = "\"Fair.\"",
                        Grates = "\"It shows.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"We're happy. Is that so bad?\"",
                        Lands = "\"Not bad. Just loud.\"",
                        Even = "\"Fine.\"",
                        Grates = "\"For your game, terrible.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who else has noticed?\"",
                        Lands = "\"Everyone. Even the ones pretending not to.\"",
                        Even = "\"Most.\"",
                        Grates = "\"Everyone.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "will.rom.react2.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 2,
                Open = "(quietly, for once) \"Careful. A pair is a target. I'd split you up myself, and I'd smile while I did it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I know. We're ready for it.\"",
                        Lands = "\"Wonderful. Then I'll watch.\"",
                        Even = "\"Hope so.\"",
                        Grates = "\"Nobody is.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's coming for us?\"",
                        Lands = "\"Me, possibly. Kidding. Mostly.\"",
                        Even = "\"Can't say.\"",
                        Grates = "\"Not telling.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Thanks for the warning.\"",
                        Lands = "\"Don't mention it. Really. Don't.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Use it.\"",
                    },
                },
            },
        };
    }
}
