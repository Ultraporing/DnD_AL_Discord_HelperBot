# What is this?
This is a Discord Bot I've written and am hosting for the [**Unofficial Roll20 D&D Adventures League Deutschland**](https://app.roll20.net/campaigns/details/2146967/d-and-d-adventurers-league-community-deutsch-slash-german) Discord Server. 
It exists to help the Admins and Mods with determining if new players followed the Rule to have the same name in the Discord Server and Roll20 game.

# What does it do?
## Description
Everything hereafter in (```code blocks```) are fields in the **config.json**.

It accepts prefixed (```prefix```) commands via DM by server (```discordServer```) members with a specific role (```whitelistRoleNames```), and ignores every message coming from people which are not on the server and do not have that role.

The commands allow it to crawl the Roll20 Game Webpage (```roll20Game```), collect all the player names and build a list of players in the game together with an UTC timestamp when the player was first discovered on the Roll20 Game.
This is then used to build another list in conjunction with all the Discord Server Member names and roles, to figure out which player is not already an adventurer (```adventurerRoleName```) and determine if their names match with the Roll20 Game Players.

## The Commands
### !listNonAdv
Viewing the non-adventurer-list which displays an red X if the names do not match with Roll20 or a green tick mark if they do.

### !listR20
Viewing the Roll20 Players list but beware this list can be very very long so be ready for spam.

### !find <name>
Search for players with exactly this name and display an red X if the names do not match with Roll20 or a green tick mark if they do.

### !findp <name>
Search for players names which partial match, display a star emoji next to a perfect match and display an red X if the names do not match with Roll20 or a green tick mark if they do.

### !update
Update the internal kept lists (cache) and re-evaluate them by pulling data from Roll20 and the Discord Server. This command can only be run once at the same time. While an update is in progress everyone sending this command will get a notice to wait and cannot send anything else while it's running.

### !help
Show the Help message to see available commands.

### !status
Show the UTC timestamp of the last update.

## Can I fork and modify the repo to create my own version?
Yes you can! I do not mind you reusing my code or use it as basis for something else. Some sort of credit would be nice, but you do not have to :).
