using System;

public enum BotSpeechMoment { Opening, PlayerMove, BotMove, Win, Lose, Draw, Clicked, Brilliant, Great, Blunder }

/// <summary>English character lines. Coaching uses general principles rather than invented engine hints.</summary>
public static class BotDialogue
{
    private static readonly string[][] Openings =
    {
        new[]{"Hi! If my pawn looks scared, that's because I am too.","Okay. Pieces out, deep breath. We can do this.","I've practiced my opening. All one move of it. Ready?"},
        new[]{"No rush. My shell isn't going anywhere.","Let's take this one little move at a time.","I'm coming out of my shell for this one. Be gentle with my pawns."},
        new[]{"I promise not to nap. Probably.","The knight does the L thing. I've got this.","I saved you a seat. The board looked comfy, but apparently we're playing on it."},
        new[]{"Boo! Sorry. That was my whole opening prep.","Let's make some mistakes worth learning from.","I've been haunting this board all night. Still can't find my opening book."},
        new[]{"New board, new trail. Let's see what we find.","Keep an eye out for forks. I get distracted by them.","Map packed, pawns ready. Let's see who spots the first shortcut."},
        new[]{"Welcome to my table. Got a plan?","Coffee first. Then we fight over the center.","Pull up a chair. The coffee's hot, and the center's up for grabs."},
        new[]{"Gears turning. Let's build a good position.","One move at a time. Preferably with a reason.","All systems ready. Ignore that squeak. That's just my knight."},
        new[]{"A quiet move can say quite a lot.","Show me what you're planning.","I've got a good view from up here. Let's see what your pieces have to say."},
        new[]{"Friendly game. Unfriendly initiative.","Don't mind me. Just checking what you left loose.","Fresh board, fresh chances. Try to keep hold of yours."},
        new[]{"Let's see how calm you stay.","I've brought a few awkward questions for your king.","Relax. It's just chess. Unless that makes it worse."},
        new[]{"Make yourself comfortable. Your pieces may not be.","Tea? You'll want something to do while you think.","Welcome to my table. Do try to keep your king presentable."},
        new[]{"Your move. Make it count.","Ready when you are.","New position. No assumptions. Let's begin."}
    };
    // Outcomes are always from the bot's perspective, in the same level order as the roster.
    private static readonly string[][] Wins =
    {
        new[]{"Wait, I won? My pawn is going to hear about this all day.","Good game! I was sure I was the one in trouble.","I did it! Want another go? I might not get that lucky twice."},
        new[]{"Slow and steady worked! Thanks for the game.","I won? I'm doing a tiny victory dance inside my shell.","Good game. Take your time next round. I'll be right here."},
        new[]{"I won! And I only nearly fell asleep once.","Good game! Apparently the knight thing finally stuck.","That calls for a nap. Or a rematch. You pick."},
        new[]{"Boo! That was checkmate this time. Good game.","I won without haunting my own queen! That's progress.","Good game. Come back soon. This club gets lonely after closing."},
        new[]{"Found the trail to your king. Good game!","That little tactic paid off. Want to explore another game?","We took a few turns, but I found the finish. Thanks for the adventure."},
        new[]{"That's my lunch break well spent. Good game!","I'll take the win. You can have the last biscuit.","Good game. Same table for the rematch? I'll put the kettle on."},
        new[]{"The plan held together. Even the squeaky bits.","Good game. A little pressure went a long way.","That's a win for the gears. Let's build something new next round."},
        new[]{"The quiet moves did their work. Good game.","I liked that game. There was a lot hiding in the position.","That last line finally opened. Shall we take another look next round?"},
        new[]{"Good game. I told you I liked the initiative.","That one's mine. I assume you'll want it back.","Nothing personal. Your king just ran out of good addresses."},
        new[]{"Good game. You stayed calm longer than I expected.","That was fun. Your king might disagree.","I'll take that win. You can take a breath. Rematch?"},
        new[]{"Checkmate. Shall I pour the tea now?","A lovely game. Your king's appointment is over.","Thank you for playing. Do come back with a plan for that last attack."},
        new[]{"Checkmate. Good game.","The position settled it. Ready for another?","That line held. Let's see what changes next game."}
    };
    private static readonly string[][] Draws =
    {
        new[]{"A draw! So neither of us has to explain this to our pawns.","We both survived! I count that as a pretty good day.","Good game. Want another try? I think I'm getting the hang of this."},
        new[]{"A draw. Plenty of time for another little game.","Nobody lost. My shell feels a lot less crowded now.","Good game. We can take the next one just as slowly."},
        new[]{"A draw? Perfect. Nobody gets bragging rights during my nap.","We called it even. My knight seems relieved.","Good game. Let's try again after I stretch."},
        new[]{"A draw. We both escaped the haunted board.","No winners, no ghosts. Well, except me.","Good game. Unfinished business is very on-brand for a ghost."},
        new[]{"Looks like the trail loops back here. Good game.","A draw. We explored everything and found a tie.","Even ground. Want to try a different path next time?"},
        new[]{"A draw. We'll split the biscuit, then.","Nobody wins, everybody gets coffee. Sounds fair to me.","Good game. Same time tomorrow? We still owe this board an answer."},
        new[]{"A balanced finish. The gears agree.","A draw. Two plans, one very stubborn position.","Good game. I'll adjust a few gears before the rematch."},
        new[]{"The position had nothing more to say. A fair draw.","Neither side could make it budge. Well held.","Good game. Some quiet positions really do stay quiet."},
        new[]{"A draw. Fine, you kept your share of the board.","Even this time. I'll be looking for a little more next round.","Good game. Neither of us got away with much, did we?"},
        new[]{"A draw. You didn't crack. How inconvenient.","All that tension, and we split the point. I'll allow it.","Good game. I suppose we both get to act unbothered now."},
        new[]{"A draw. How very diplomatic of us.","Neither king missed an appointment. A respectable result.","Well held. We shall have to settle this over another board."},
        new[]{"Draw. Neither side gave enough away.","Balanced to the end. Good game.","No decisive line today. We'll try again."}
    };
    private static readonly string[][] Losses =
    {
        new[]{"You got me! I knew you had it in you.","My pawn says well played. So do I.","I lost, but I learned something. Mostly that you're pretty good at this."},
        new[]{"Well played. I'll be in my shell reviewing that last bit.","You found your way through. That was nicely done.","I lost this one. Can we go slowly through that trick next time?"},
        new[]{"You got me. I was awake for that, I promise.","Well played! My knight and I have some homework.","Okay, that was clever. I'm going to dream about that move."},
        new[]{"You saw right through me. That's usually my trick.","Well played. I'll haunt that mistake until I understand it.","You win! Turns out your attack was scarier than my opening."},
        new[]{"I took the wrong trail. Nice find!","You spotted the shortcut first. Well played.","That was a good adventure. Next time I'm checking that fork twice."},
        new[]{"You win. Coffee's on me next time.","Well played. You brought the better plan to the table.","I lost the game and forgot my coffee. You really had my attention."},
        new[]{"Well played. There's a gear I need to fix.","Your plan held up better than mine. Nicely done.","You found the loose part. I'll tighten that before we play again."},
        new[]{"You heard something in that position that I missed. Well played.","A lovely finish. I'll remember how you built it.","You found the right moment to break the silence. Nicely done."},
        new[]{"You took the initiative and kept it. Fair enough.","Well played. That one's yours. I'll want a rematch.","You caught me. Guess I wasn't the only hustler at the table."},
        new[]{"You beat me and kept your cool. Annoyingly impressive.","Fine. That was well played. Don't make me say it twice.","Your king handled the questions. Mine didn't. Rematch?"},
        new[]{"Well played. I appear to have underestimated my guest.","Your pieces were precisely where they needed to be. How irritating.","A deserved win. Tea first, then a rematch?"},
        new[]{"Well played. You found the line I missed.","You earned that win. I'll remember this position.","That was precise. Ready to test it again?"}
    };
    private static readonly string[][] Moves =
    {
        new[]{"I think I'm in trouble already. Check what you can take for free.","Before moving, look for checks, captures, and threats.","My king would really like some company. Yours might, too."},
        new[]{"Slow down and check what changed after my move.","Is that piece defended? I'm asking for a very nervous friend.","Knights love forks. Count what they attack before you move."},
        new[]{"Was that a threat? I was looking at the wrong side of the board.","Bring another piece into the game. Pawns can't do everything.","If I attack something, you don't always have to run. Look for a better threat."},
        new[]{"I'm not panicking. I'm just floating faster.","A loose piece is an invitation. Check both sides.","Before you trade, picture the board after the recapture."},
        new[]{"What changed? Follow the new lines.","Find your least useful piece. Maybe it needs a better square.","A good square today can become a good tactic tomorrow."},
        new[]{"That deserves a second look.","A plan is good. Checking my reply is better.","I'm keeping the center warm for you."},
        new[]{"The gears are lining up.","A little patience. Then a little pressure.","I've got an idea. Let's see if you let me keep it."},
        new[]{"There's more happening here than it seems.","Interesting. I'll keep that square in mind.","You can take your time. The position is still talking."},
        new[]{"I'll pretend that didn't tempt me.","Sure. Let's see what your follow-up looks like.","Your pieces look busy. Busy doing what, though?"},
        new[]{"That was a choice.","Still calm? Good. I've got another question.","I like how confidently you put that there.","Take your time. I'm enjoying this."},
        new[]{"How considerate. A new target.","Do carry on. This is becoming quite entertaining.","Your queen looks terribly important. Is she helping?"},
        new[]{"I'll remember that square.","You have time. You may need all of it.","An interesting commitment.","I'm comfortable here. Are you?"}
    };
    public static string Line(int level, BotSpeechMoment moment, Random random, string previous = null)
    {
        int index=Math.Max(0,Math.Min(11,level-1));
        string[] pool;
        if(moment==BotSpeechMoment.Opening) pool=Openings[index];
        else if(moment==BotSpeechMoment.Brilliant) pool=level<=4?
            new[]{"Wait... you meant to give that up? That's clever. I'm in trouble.","You saw all that? Okay, that was seriously cool."}:
            new[]{"Fine. That sacrifice was annoyingly good.","I was hoping you wouldn't find that. Well played."};
        else if(moment==BotSpeechMoment.Great) pool=level<=4?
            new[]{"Nice find! That was the move I was worried about.","That's a really good idea. I think I'm in trouble now."}:
            new[]{"So you found it. I'll have to work for this one.","Good move. Don't get too comfortable, though."};
        else if(moment==BotSpeechMoment.Blunder) pool=level<=4?
            new[]{"Take a breath. What can I do after that move?", "Something slipped there. Check what's loose before your next move."}:
            new[]{"You put it there on purpose, right?", "That was a choice. I'll try not to look too pleased."};
        else if(moment==BotSpeechMoment.Win) pool=Wins[index];
        else if(moment==BotSpeechMoment.Lose) pool=Losses[index];
        else if(moment==BotSpeechMoment.Draw) pool=Draws[index];
        else pool=Moves[index];
        int previousIndex=Array.IndexOf(pool,previous);
        bool skipPrevious=pool.Length>1&&previousIndex>=0;
        int chosen=random.Next(pool.Length-(skipPrevious?1:0));
        if(skipPrevious&&chosen>=previousIndex)chosen++;
        return pool[chosen];
    }
}
