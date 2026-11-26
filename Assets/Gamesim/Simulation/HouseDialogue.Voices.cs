using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The house's other voices. Five houseguests from the web game speak lines written for each of them. Everyone else -
    /// the other regulars, the All-Stars, a custom houseguest - used to share one plain line in every slot, so in one week
    /// a whole house opened a conversation with the same sentence. Now each of them speaks in the voice their traits give
    /// them (<see cref="Voice.Of"/>, the voice their vote answers are in): three lines a slot in each voice. The plain
    /// voice's lines are the ones written in place in HouseDialogue.cs, with two more here for a slot of one line.
    ///
    /// <para>Which of the three: the week turns it, as it turns the five's own lines, so nobody says the same thing two
    /// weeks running; a keyed story draw (<see cref="StoryRandom.Index"/>, a hash of the season's seed, never a roll) sets
    /// where each season starts; and where a houseguest stands among the housemates who share their voice moves them
    /// along, so two of one voice never say the same line in the same week while the slot has one neither is using. It
    /// reads the state and writes nothing.</para>
    ///
    /// <para>Anyone in a voice can say its lines, the All-Stars among them, so every line stays in the game's register and
    /// says nothing about anyone's life outside the house. A nominee's eviction speech is not here: it is saved.</para>
    /// </summary>
    public static partial class HouseDialogue
    {
        /// <summary>The slots the voices' bank fills, in the order HouseDialogue.cs speaks them.</summary>
        public static IReadOnlyList<string> VoicedSlots => Array.AsReadOnly(SlotOrder);

        /// <summary>
        /// The bank's lines for one slot in one voice: three for Blunt, Warm, Chatty and Guarded. The plain voice's lines are
        /// written in place in HouseDialogue.cs, so for Plain this is only the two extra lines of a slot of one line. Empty
        /// for a slot or voice the bank does not hold.
        /// </summary>
        public static IReadOnlyList<string> VoicedLines(string slot, string voice)
        {
            if (slot != null && voice != null && Bank.TryGetValue(slot, out var voices) && voices.TryGetValue(voice, out var lines))
                return Array.AsReadOnly(lines);
            return Array.Empty<string>();
        }

        /// <summary>Every line the bank holds for one slot, in every voice.</summary>
        public static IReadOnlyList<string> VoicedLines(string slot) =>
            Voice.All.SelectMany(voice => VoicedLines(slot, voice)).ToList().AsReadOnly();

        /// <summary>The five houseguests with lines of their own (<see cref="Pick"/>'s cases).</summary>
        private static bool OwnVoice(string canonicalId)
        {
            switch (canonicalId)
            {
                case "maya-hassan":
                case "taylor-kim":
                case "jamie-roberts":
                case "casey-wilson":
                case "riley-johnson":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// A slot of three variants: one of the five says their own line, by week, as always; anyone else says one of
        /// their voice's lines. <paramref name="first"/> to <paramref name="third"/> are what <see cref="Pick"/> gave, so
        /// for anyone outside the five they are the plain voice's lines.
        /// </summary>
        private static string Voiced(EpisodeState state, ContestantState speaker, string slot, string first, string second, string third)
        {
            if (speaker == null || OwnVoice(ContentCatalog.CanonicalId(speaker.id))) return Vary(state, first, second, third);
            return FromBank(state, speaker, slot, new[] { first, second, third });
        }

        /// <summary>A slot of one line: one of the five says it; anyone else says one of their voice's.</summary>
        private static string Voiced(EpisodeState state, ContestantState speaker, string slot, string only)
        {
            if (speaker == null || OwnVoice(ContentCatalog.CanonicalId(speaker.id))) return only;
            return FromBank(state, speaker, slot, new[] { only });
        }

        private static string FromBank(EpisodeState state, ContestantState speaker, string slot, string[] plain)
        {
            string voice = Voice.Of(speaker, false);
            var lines = new List<string>();
            if (voice == Voice.Plain) lines.AddRange(plain);
            lines.AddRange(VoicedLines(slot, voice));
            if (lines.Count == 0) lines.AddRange(plain);
            int turn = state.week > 0 ? state.week - 1 : 0;
            int start = StoryRandom.Index(state, "line:" + slot + ":" + voice, lines.Count);
            return lines[(turn + start + Rank(state, speaker, voice)) % lines.Count];
        }

        /// <summary>How many housemates outside the five, in the house and sharing this voice, come before the speaker by id.</summary>
        private static int Rank(EpisodeState state, ContestantState speaker, string voice)
        {
            int rank = 0;
            foreach (var other in state.contestants)
                if (other != null && !ReferenceEquals(other, speaker) && !other.isPlayer && other.status == ContestantStatus.Active
                    && string.CompareOrdinal(other.id, speaker.id) < 0 && !OwnVoice(ContentCatalog.CanonicalId(other.id))
                    && Voice.Of(other, false) == voice)
                    rank++;
            return rank;
        }

        private static Dictionary<string, string[]> Slot(string[] blunt, string[] warm, string[] chatty, string[] guarded, string[] plain = null)
        {
            var voices = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { Voice.Blunt, blunt }, { Voice.Warm, warm }, { Voice.Chatty, chatty }, { Voice.Guarded, guarded },
            };
            if (plain != null) voices.Add(Voice.Plain, plain);
            return voices;
        }

        private static readonly string[] SlotOrder =
        {
            "ceremony", "block", "opening", "talked", "member-no", "cut-ties", "left-goes-on", "left-ended", "safety",
            "final-two", "vote", "shared", "broken", "kept", "allies", "wary", "trusted", "unknown", "pact-yes", "pact-no",
            "window-nominate", "window-veto", "window-replace",
        };

        private static readonly Dictionary<string, Dictionary<string, string[]>> Bank =
            new Dictionary<string, Dictionary<string, string[]>>(StringComparer.Ordinal)
            {
                // Greeting, outside free time and the campaign: not now.
                {
                    "ceremony", Slot(
                        blunt: new[] { "Not now. Ceremony first, talk second.", "Bad timing. Find me once this is over.", "We'll talk when the house isn't watching a ceremony. Later." },
                        warm: new[] { "I want to give you my full attention, and I can't right now. Later?", "Can we wait until this is over? I'd hate to rush you.", "Hold that thought for me. We'll sit down properly once the ceremony's done." },
                        chatty: new[] { "Oh, I would love to, but everyone's watching the ceremony. Later, I promise.", "So much to say, zero time to say it. Catch me once this wraps up?", "Ask me again when this is over. I'll have notes, trust me." },
                        guarded: new[] { "Later.", "Not during the ceremony.", "When this is done. Not before." })
                },
                // Greeting from the block, in the campaign.
                {
                    "block", Slot(
                        blunt: new[] { "I'm on the block. I'm not begging, but you should hear my side.", "Nominated. Fine. Ask me straight and I'll answer straight.", "My name's up there. I want to stay, so I'll tell you why I should." },
                        warm: new[] { "I'm on the block, and I'm scared, honestly. Could we talk for a bit?", "Being nominated is rough. I'd really like you to hear me out.", "This week's been hard. I just want a fair chance to explain myself." },
                        chatty: new[] { "So I'm nominated, which, wow. Can I tell you my side before everyone else tells theirs?", "Okay, the block. Everybody has a story about me this week. Want to hear mine?", "I'm on the block and suddenly the whole house is whispering. Talk to me instead?" },
                        guarded: new[] { "I'm nominated. Hear me out.", "On the block. I'd like a word.", "My name's up. I'll keep it short." })
                },
                // Greeting in free time and the campaign: the opening, before where they stand.
                {
                    "opening", Slot(
                        blunt: new[] { "Good, you found me. Let's skip the small talk.", "You want to talk game? Then let's talk game.", "I'd rather hear it from you than from the kitchen." },
                        warm: new[] { "Hey, you. Come sit with me for a minute.", "I was hoping I'd get a moment with you today.", "Take a breath with me. This house never stops." },
                        chatty: new[] { "Oh good, it's you. Did you hear what happened in the kitchen? Never mind, later.", "So, the whole house is buzzing today. Your turn to talk to me.", "Okay, I have about six things to tell you, but you first." },
                        guarded: new[] { "You wanted a word.", "Go on. I'm listening.", "Keep it between us." })
                },
                // The reply to time spent talking.
                {
                    "talked", Slot(
                        blunt: new[] { "Good. No fluff, just what we needed.", "That was worth the time. Let's do it again.", "Straight talk. I like that." },
                        warm: new[] { "I really needed that. Thank you for sitting with me.", "That meant a lot. It's nice to feel heard in here.", "Thank you. I feel a little lighter now." },
                        chatty: new[] { "Okay, that was fun. I talked too much, didn't I? Don't answer.", "See, this is why I like talking to you. Easy, no drama.", "Good chat. I could do that all day, honestly." },
                        guarded: new[] { "Good. That helped.", "Thanks for the time.", "Useful. Thanks." })
                },
                // Brought into a pact, and a member would not have them.
                {
                    "member-no", Slot(
                        blunt: new[] { "They said no to me? Their loss.", "Fine. If they don't want me, I don't need them.", "Shot down by your own pact. Tell them I noticed." },
                        warm: new[] { "That stings a little. Thank you for wanting me in, though.", "It's okay. I know you tried. That's what matters to me.", "I won't pretend that doesn't hurt. Thanks for asking anyway." },
                        chatty: new[] { "Wait, they voted me down? Wow. Okay. I'm fine. I'm totally fine.", "Well, that's a story for the hammock later. Thanks for trying.", "So I'm not invited. Noted, filed, and definitely not forgotten." },
                        guarded: new[] { "They don't want me. Understood.", "No from them. Fine.", "So be it." },
                        plain: new[] { "They aren't ready to have me in. That's all right. Thanks anyway.", "Sounds like it's a no from them. I appreciate you asking." })
                },
                // The player cut ties the week this houseguest turned on the pact.
                {
                    "cut-ties", Slot(
                        blunt: new[] { "Yeah, I earned that. No complaints.", "Fair. I swung first.", "I'd have done it too. We're square." },
                        warm: new[] { "I understand. I hurt you first, and I'm sorry for that.", "That's fair. I wish I'd handled it differently.", "I don't blame you. I'd want distance from me too." },
                        chatty: new[] { "Okay, yes, I saw that coming. Honestly, I'd have done the exact same.", "Well, I made that bed. I'll lie in it. Loudly.", "So that's the end of us. I'd be lying if I said I didn't expect it." },
                        guarded: new[] { "Understood. My doing.", "Fair.", "I expected that." },
                        plain: new[] { "That's fair. I'm the one who broke it first.", "I understand. I made that choice, and this is what it cost." })
                },
                // The player left a pact that goes on without them: it goes on, never that it is over.
                {
                    "left-goes-on", Slot(
                        blunt: new[] { "You walked. We're still standing. Your call.", "We'll keep the pact going. Don't come knocking later.", "You're out; we're not. Remember that." },
                        warm: new[] { "It's sad to see you go. We'll keep it going, but I'll miss you in it.", "We're staying together, the rest of us. I hope you're okay.", "I wish you'd stayed. We'll carry on, and I'll still say hi in the kitchen." },
                        chatty: new[] { "So you left, and the rest of us are still a team. Awkward, honestly.", "Okay, you're out, we're in. The house is going to talk about this for days.", "You left us? Fine, we'll keep going. Bit of a plot twist, though." },
                        guarded: new[] { "You left. We stay.", "The pact goes on. Without you.", "Noted. We carry on." })
                },
                // The player left, and the pact between them is over.
                {
                    "left-ended", Slot(
                        blunt: new[] { "So we're done. Fine. I'll play my own game.", "Alliance over. No hard feelings, no favors either.", "You ended it. I'll stop covering for you." },
                        warm: new[] { "Our alliance is over, then. I'm sad, but I'm glad you told me.", "That hurts a bit. I'll still be kind to you, though.", "I'll miss what we had. Thank you for being honest about it." },
                        chatty: new[] { "Wow, okay, our alliance is officially over. The house is going to love this.", "So we're broken up. Strategically. I'm fine. Totally fine.", "Done, just like that? I need a minute. And a snack." },
                        guarded: new[] { "Then it's over.", "Our alliance is done. Understood.", "No more alliance. Noted." })
                },
                // A safety promise, heard: never that it makes them safe.
                {
                    "safety", Slot(
                        blunt: new[] { "You said I'm safe. Don't make me regret believing you.", "Safety promised. I'll be watching to see you keep it.", "Keep me off the block when it's up to you. That's the deal." },
                        warm: new[] { "Thank you for promising that. It really helps me breathe.", "That's a kind promise. I'll remember you made it.", "I believe you meant it. That's all I can ask." },
                        chatty: new[] { "A safety promise! Okay, I'm writing that down. Mentally. In ink.", "You'll keep me safe? I'm going to remind you of that. Often.", "Safety, huh? Good. I'll tell myself that every time the house gets weird." },
                        guarded: new[] { "Safety. Noted.", "I'll hold you to it.", "Your word. I'll remember it." })
                },
                // A final-two promise, heard: never that it decides the end.
                {
                    "final-two", Slot(
                        blunt: new[] { "Final two. Then we both have to win our way there.", "You said final two. Don't flinch when it counts.", "Final two with me. Mean it or don't say it." },
                        warm: new[] { "Final two? That's a big thing to promise. I'm touched.", "I'd love to sit next to you at the end. Thank you.", "Thank you for trusting me that far. I won't forget it." },
                        chatty: new[] { "Final two! Okay, I'm picturing the finale already. Is that weird?", "You and me at the end? The house would never see it coming.", "Final two, wow. I'm not telling a soul. Okay, maybe one soul. Kidding." },
                        guarded: new[] { "Final two. We'll see.", "A long way off. Noted.", "Final two. Remembered." })
                },
                // A vote promise, heard: one ballot, never the house's.
                {
                    "vote", Slot(
                        blunt: new[] { "You told me your vote. Stick to it.", "One vote, promised. Keep it that way.", "Your vote's your word now. Don't break it." },
                        warm: new[] { "Thank you for telling me how you'll vote. That's comforting.", "I'm grateful. I know it's only your vote, and that's enough.", "That helps me sleep tonight. Thank you." },
                        chatty: new[] { "Your vote! Okay, that's one. Now I just need to count everyone else's.", "Your vote's promised? Love that. One down, the rest of the house to go.", "Okay, your vote is locked in my head now. No take-backs." },
                        guarded: new[] { "Your vote. Noted.", "One vote. Understood.", "I heard you. One vote." })
                },
                // Something the player told them: their account, never a fact.
                {
                    "shared", Slot(
                        blunt: new[] { "Thanks. If it's wrong, I'll find out.", "Noted. I don't take anything on faith in here.", "Interesting. I'll see if it holds." },
                        warm: new[] { "I appreciate you letting me in on that. I'll handle it gently.", "That's kind of you to share. I won't jump to conclusions.", "Thank you for trusting me with it. I'll keep an open mind." },
                        chatty: new[] { "Ooh, really? Okay, that explains a lot. Or nothing. I'll find out.", "Wait, tell me that again later. I want to see who else knows.", "That's juicy. I'll weigh it before I believe a word." },
                        guarded: new[] { "Heard. I'll weigh it.", "Your account. Noted.", "I'll think on it." })
                },
                // Where they stand: the player broke a promise to them.
                {
                    "broken", Slot(
                        blunt: new[] { "You promised and didn't deliver. I don't forget that.", "You broke a promise. Your next one costs more.", "I trusted your word once. That's done until you earn it back." },
                        warm: new[] { "That broken promise still hurts. I want to trust you again, though.", "I was counting on you, and it didn't hold. I'm trying to let it go.", "It hurt when that promise broke. Let's be gentle with each other." },
                        chatty: new[] { "So, the promise. The one you broke. Yes, I'm still thinking about it.", "Everyone in here breaks promises, but yours was to me. That one stung.", "Remember that promise? I do. Vividly. Every detail." },
                        guarded: new[] { "You broke a promise. I remember.", "Your word didn't hold. I'm careful now.", "One broken promise. That's enough." })
                },
                // Where they stand: the player kept a promise to them.
                {
                    "kept", Slot(
                        blunt: new[] { "You kept your word. That counts with me.", "You delivered. I respect that.", "Promise kept. That's how it's done." },
                        warm: new[] { "You came through for me. That meant the world.", "Thank you for keeping your word. I feel safer with you around.", "You did what you said you would. That's rare and lovely." },
                        chatty: new[] { "You actually kept your promise! Do you know how rare that is in here?", "Promise kept! Honestly, I'm a little stunned. In a good way.", "You did what you said. Someone write that on the fridge." },
                        guarded: new[] { "You kept your word. Noted.", "Promise kept. I won't forget.", "You followed through. Good." })
                },
                // Where they stand: in a pact with the player.
                {
                    "allies", Slot(
                        blunt: new[] { "We're allies. So tell me who we're going after.", "Allies don't hold back. What do you know?", "We're on the same side. Let's act like it." },
                        warm: new[] { "I'm so glad we're in this together. How's your week going?", "Having you as an ally makes this place easier. Talk to me.", "We look out for each other, right? Tell me what you need." },
                        chatty: new[] { "Alliance meeting! Okay, quick, what have you heard today?", "So, partner, what's the latest? I've got news too.", "Plotting in plain sight again. I love it. What's new?" },
                        guarded: new[] { "Allies. Anything new?", "Same side. Keep me informed.", "We're aligned. Talk." })
                },
                // Where they stand: wary of the player.
                {
                    "wary", Slot(
                        blunt: new[] { "I don't trust you right now. Change my mind.", "I'm keeping you at arm's length. Say what you came to say.", "I'm not sold on you. Prove me wrong." },
                        warm: new[] { "I'm feeling a little unsure about us. Can we be honest with each other?", "I want to trust you, I really do. I'm just not there yet.", "I'm being careful with you right now. I hope that changes." },
                        chatty: new[] { "Okay, full honesty? I'm not sure about you. Convince me.", "So, I'm keeping a little distance from you right now. Nothing personal. Mostly.", "I'll be honest, you make me nervous. Talk me down." },
                        guarded: new[] { "Careful. I don't trust you yet.", "I'll listen. Not much more.", "You'll have to earn it." })
                },
                // Where they stand: they trust the player.
                {
                    "trusted", Slot(
                        blunt: new[] { "You're one of the few I trust in here. Let's use it.", "I like where we stand. What's the move?", "You've earned my trust. Don't waste it." },
                        warm: new[] { "I always feel better when you stop by. What's up?", "You're my calm in this house. Sit with me.", "I trust you. It's nice to say that out loud." },
                        chatty: new[] { "Oh, my favorite person to gossip with. Sit, sit.", "Finally, someone I can actually talk to. So, what's new?", "You get me, you know? Okay, what have you got?" },
                        guarded: new[] { "I trust you. Go on.", "You're solid. Talk.", "Good. It's you." })
                },
                // Where they stand: still getting to know the player.
                {
                    "unknown", Slot(
                        blunt: new[] { "I don't know you yet. Tell me what you're playing for.", "We haven't sized each other up. So, what's your game?", "Tell me something real. I'll decide what I make of you." },
                        warm: new[] { "I'd love to know you better. What's your favorite part of the house?", "I feel like I barely know you. Let's fix that.", "Tell me about you. Not the game, just you." },
                        chatty: new[] { "So we've barely talked! Tell me everything. Start anywhere.", "I know everyone's business in here except yours. Spill.", "Okay, I want the full story on you. Go." },
                        guarded: new[] { "Don't know you yet.", "Still deciding about you.", "We'll see about you." })
                },
                // A pact's yes.
                {
                    "pact-yes", Slot(
                        blunt: new[] { "Deal. We play to win, both of us.", "We're a team now. Don't make me regret it.", "Alliance it is. I've got your back if you've got mine." },
                        warm: new[] { "Yes! I'm so happy we're doing this together.", "I'm really glad you asked me. We'll look after each other.", "This feels right. Thank you for wanting me with you." },
                        chatty: new[] { "Oh, we're official now? I love this. Do we need a handshake?", "An alliance! Okay, I'm already thinking of names. Kidding. Sort of.", "Yes, yes, absolutely. The house won't know what hit it." },
                        guarded: new[] { "Agreed. Between us.", "Fine. We're allied.", "Yes. Quietly." })
                },
                // A pact's no, said without a reason.
                {
                    "pact-no", Slot(
                        blunt: new[] { "That's a no from me.", "I'm passing. Don't take it personally.", "Not happening this week." },
                        warm: new[] { "Oh, I hate saying this, but no. Not right now.", "I'm not ready for that. I hope we're still okay.", "Thank you for asking. I have to say no, though." },
                        chatty: new[] { "Ooh, tempting. But no. Ask me again some other time?", "An alliance? I'm going to say no and then think about it all night.", "No, but I'm flattered. Really. Tell no one I said no." },
                        guarded: new[] { "No.", "Not now.", "I'll pass." })
                },
                // The Head of Household, before nominations.
                {
                    "window-nominate", Slot(
                        blunt: new[] { "My nominations, my call. Make it quick and make it good.", "Two names. Give me a reason yours isn't one of them.", "You've got my ear. Don't waste it." },
                        warm: new[] { "This part is so hard. Come tell me what's on your mind.", "I know it's a scary week. Talk to me, I'm listening.", "I want to get this right. Help me think it through." },
                        chatty: new[] { "Oh, the visits never stop this week. Okay, your turn. Go.", "So, nominations. Everyone has an opinion. Let's hear yours.", "Come in, close the door. Who's been saying what?" },
                        guarded: new[] { "Nominations are mine. Speak.", "I'm deciding. Say your piece.", "Make your case." },
                        plain: new[] { "I'm naming two people soon. Tell me what you think.", "Nominations are on me this week. What should I know?" })
                },
                // The veto holder, before the meeting.
                {
                    "window-veto", Slot(
                        blunt: new[] { "I've got the veto. Tell me why I should care.", "The veto's mine. Pitch it straight.", "Use it or keep it. Convince me." },
                        warm: new[] { "Holding the veto is heavier than I thought. What do you think I should do?", "I want to use this power kindly. Tell me what you need.", "Everyone's counting on me with this veto. Talk me through it." },
                        chatty: new[] { "The veto! Everyone suddenly loves me. So, what's your take?", "Okay, the whole house has a plan for my veto. What's yours?", "So many opinions about one little medallion. Go ahead, add yours." },
                        guarded: new[] { "The veto's mine. Talk.", "I haven't decided. Go on.", "Say it. Briefly." },
                        plain: new[] { "I hold the veto this week. What do you think I should do?", "The veto decision is mine. I'm open to hearing you out." })
                },
                // The Head of Household, before the veto meeting, about a replacement.
                {
                    "window-replace", Slot(
                        blunt: new[] { "A replacement nominee is on me. Choose your words.", "Replacement's my call. Give me a name or give me a reason.", "If the veto's used, I won't hesitate. Talk fast." },
                        warm: new[] { "I really hope I don't have to name anyone else. Talk to me.", "Naming a replacement would break my heart. What should I know?", "If it comes to a replacement, I want to be fair. Help me." },
                        chatty: new[] { "If the veto gets used, I need a new name, and everyone's lobbying. Your turn.", "Replacement nominee, ugh. Okay, who's the house whispering about?", "If someone comes off the block, guess who's in the hot seat? Me. Talk." },
                        guarded: new[] { "A replacement would be mine. Go on.", "If the veto's used, I choose. Speak.", "I'm listening. For now." },
                        plain: new[] { "If someone comes off the block, I name the replacement. What should I know?", "A replacement nominee would be my call. Tell me where you stand." })
                },
            };
    }
}
