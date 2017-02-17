# Prophunt

This is an unfinished prophunt gamemode for GTA Network. As it is currently a work in progess, I'm welcoming contributors to finish it by submitting pull requests.

## Usage

The allowed props are saved in a file called `props.txt`. Each line contains the string for a model hash. Lines that start with a `#` are comments and are ignored.

There's a couple debug commands you can use while developing/testing:

* `/setprop <num>` will change your prop to the given index. This index corresponds to an occurance in `props.txt` (note: not specifically a line number!)
* `/begin` forcefully begins a round, even if there are no players in the session. This is so you can test with only 1 player.
* `/state <state>` will change the current game state and process any necessary changes required. This state can be one of the following:
  * `Waiting` Waiting for players to join.
  * `Hiding` The hiders have time to hide while the seekers are blinded.
  * `Seeking` The seekers are unblinded and have to find the hiders.
  * `EndOfRound` Either the time limit was reached, all the hiders were found (killed), or all the seekers died.
* `/end` will forcefully end the game, even if there are enough players to start a new round.

## Development

Note that TypeScript is used to generate the clientside JavaScript using `tsconfig.json`. It also references [Eraknelo's TypeScript definitions](https://github.com/Rene-Sackers/gta-network-typescript) which is available on NuGet.

## Installation

Just wait until it's finished. If you're going to contribute, you know how to install this already.
