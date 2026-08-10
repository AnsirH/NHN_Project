window.BALANCE_RESULT = window.BALANCE_RESULT || {};
window.BALANCE_RESULT["role_archer_vs_assassin:archer-nerf"] = {
  "scenarioName": "role_archer_vs_assassin",
  "scenarioPath": "Tools/BalanceLab/scenarios/matrix/role_archer_vs_assassin.json",
  "tag": "archer-nerf",
  "appliedOverrides": {
    "note": "\uAD81\uC218 \uC0AC\uAC70\uB9AC\u00B7\uD654\uB825 \uD558\uD5A5 1\uC548",
    "squads": {
      "Archer": {
        "attackRange": 6,
        "attackDamage": 8
      }
    }
  },
  "appliedOverridesPath": "Tools/BalanceLab/tuning/archer-nerf.json",
  "replaySeeds": [
    17
  ],
  "band": "role_matchup",
  "verdict": {
    "bandId": "role_matchup",
    "metric": "leftWinRate",
    "value": 0.59,
    "minWinRate": 0.3,
    "maxWinRate": 0.7,
    "passed": true,
    "detail": null
  },
  "runs": 200,
  "leftWins": 118,
  "rightWins": 82,
  "draws": 0,
  "leftWinRate": 0.59,
  "rightWinRate": 0.41,
  "elapsedMs": 1840,
  "battles": [
    {
      "seed": 0,
      "winner": "right",
      "ticks": 259,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 12,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 1,
      "winner": "right",
      "ticks": 239,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 15,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 2,
      "winner": "right",
      "ticks": 256,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 3,
      "winner": "left",
      "ticks": 350,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 4,
      "winner": "left",
      "ticks": 314,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 6,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 5,
      "winner": "right",
      "ticks": 262,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 6,
      "winner": "left",
      "ticks": 312,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 7,
      "winner": "left",
      "ticks": 311,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 8,
      "winner": "right",
      "ticks": 237,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 18,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 9,
      "winner": "right",
      "ticks": 328,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 6,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 10,
      "winner": "left",
      "ticks": 263,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 11,
      "winner": "right",
      "ticks": 265,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 12,
      "winner": "left",
      "ticks": 263,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 13,
      "winner": "right",
      "ticks": 260,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 10,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 14,
      "winner": "left",
      "ticks": 307,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 15,
      "winner": "right",
      "ticks": 242,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 15,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 16,
      "winner": "left",
      "ticks": 259,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 17,
      "winner": "right",
      "ticks": 317,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 5,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 4
        }
      ]
    },
    {
      "seed": 18,
      "winner": "left",
      "ticks": 377,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 19,
      "winner": "left",
      "ticks": 310,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 20,
      "winner": "left",
      "ticks": 314,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 21,
      "winner": "left",
      "ticks": 302,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 6,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 22,
      "winner": "left",
      "ticks": 253,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 23,
      "winner": "right",
      "ticks": 270,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 24,
      "winner": "left",
      "ticks": 309,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 25,
      "winner": "left",
      "ticks": 309,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 26,
      "winner": "left",
      "ticks": 263,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 27,
      "winner": "left",
      "ticks": 312,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 28,
      "winner": "left",
      "ticks": 309,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 5,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 29,
      "winner": "left",
      "ticks": 302,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 6,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 30,
      "winner": "left",
      "ticks": 312,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 31,
      "winner": "right",
      "ticks": 494,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 1,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 4
        }
      ]
    },
    {
      "seed": 32,
      "winner": "left",
      "ticks": 306,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 33,
      "winner": "left",
      "ticks": 267,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 10,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 34,
      "winner": "right",
      "ticks": 296,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 6,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 35,
      "winner": "right",
      "ticks": 285,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 36,
      "winner": "right",
      "ticks": 265,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 5,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 37,
      "winner": "left",
      "ticks": 256,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 6,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 38,
      "winner": "right",
      "ticks": 246,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 12,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 39,
      "winner": "left",
      "ticks": 261,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 40,
      "winner": "right",
      "ticks": 306,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 2,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 41,
      "winner": "left",
      "ticks": 264,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 42,
      "winner": "left",
      "ticks": 312,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 43,
      "winner": "right",
      "ticks": 250,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 12,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 44,
      "winner": "left",
      "ticks": 356,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 6,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 45,
      "winner": "right",
      "ticks": 246,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 11,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 46,
      "winner": "left",
      "ticks": 261,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 47,
      "winner": "right",
      "ticks": 263,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 4
        }
      ]
    },
    {
      "seed": 48,
      "winner": "right",
      "ticks": 271,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 49,
      "winner": "right",
      "ticks": 266,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 4
        }
      ]
    },
    {
      "seed": 50,
      "winner": "left",
      "ticks": 309,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 51,
      "winner": "right",
      "ticks": 269,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 3,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 52,
      "winner": "right",
      "ticks": 252,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 53,
      "winner": "right",
      "ticks": 267,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 54,
      "winner": "left",
      "ticks": 264,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 55,
      "winner": "left",
      "ticks": 361,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 56,
      "winner": "left",
      "ticks": 304,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 57,
      "winner": "left",
      "ticks": 310,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 5,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 58,
      "winner": "right",
      "ticks": 391,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 3,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 59,
      "winner": "right",
      "ticks": 239,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 10,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 60,
      "winner": "left",
      "ticks": 262,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 61,
      "winner": "right",
      "ticks": 238,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 12,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 62,
      "winner": "right",
      "ticks": 303,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 5,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 63,
      "winner": "left",
      "ticks": 261,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 64,
      "winner": "right",
      "ticks": 333,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 3,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 65,
      "winner": "left",
      "ticks": 447,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 3,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 66,
      "winner": "left",
      "ticks": 308,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 67,
      "winner": "right",
      "ticks": 277,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 68,
      "winner": "right",
      "ticks": 257,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 3,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 69,
      "winner": "left",
      "ticks": 354,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 4,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 70,
      "winner": "left",
      "ticks": 310,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 71,
      "winner": "left",
      "ticks": 302,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 72,
      "winner": "left",
      "ticks": 311,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 73,
      "winner": "right",
      "ticks": 304,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 5,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 4
        }
      ]
    },
    {
      "seed": 74,
      "winner": "right",
      "ticks": 238,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 15,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 75,
      "winner": "right",
      "ticks": 250,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 10,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 76,
      "winner": "left",
      "ticks": 354,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 77,
      "winner": "left",
      "ticks": 312,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 78,
      "winner": "left",
      "ticks": 311,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 79,
      "winner": "left",
      "ticks": 400,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 3,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 1
        }
      ]
    },
    {
      "seed": 80,
      "winner": "right",
      "ticks": 331,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 6,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 81,
      "winner": "left",
      "ticks": 259,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 82,
      "winner": "right",
      "ticks": 282,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 5,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 83,
      "winner": "left",
      "ticks": 396,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 3,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 84,
      "winner": "right",
      "ticks": 237,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 10,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 85,
      "winner": "left",
      "ticks": 315,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 86,
      "winner": "left",
      "ticks": 263,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 87,
      "winner": "left",
      "ticks": 257,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 88,
      "winner": "right",
      "ticks": 298,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 4,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 89,
      "winner": "right",
      "ticks": 302,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 5,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 90,
      "winner": "left",
      "ticks": 304,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 5,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 91,
      "winner": "right",
      "ticks": 246,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 13,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 92,
      "winner": "left",
      "ticks": 264,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 93,
      "winner": "left",
      "ticks": 263,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 11,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 94,
      "winner": "right",
      "ticks": 364,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 95,
      "winner": "left",
      "ticks": 347,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 2,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 4
        }
      ]
    },
    {
      "seed": 96,
      "winner": "left",
      "ticks": 310,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 97,
      "winner": "left",
      "ticks": 213,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 98,
      "winner": "left",
      "ticks": 396,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 2,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 4
        }
      ]
    },
    {
      "seed": 99,
      "winner": "left",
      "ticks": 408,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 5,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 1
        }
      ]
    },
    {
      "seed": 100,
      "winner": "right",
      "ticks": 351,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 3,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 101,
      "winner": "left",
      "ticks": 264,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 102,
      "winner": "left",
      "ticks": 263,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 103,
      "winner": "right",
      "ticks": 259,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 12,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 104,
      "winner": "right",
      "ticks": 249,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 105,
      "winner": "right",
      "ticks": 242,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 10,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 106,
      "winner": "left",
      "ticks": 303,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 107,
      "winner": "left",
      "ticks": 352,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 108,
      "winner": "left",
      "ticks": 313,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 109,
      "winner": "left",
      "ticks": 264,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 110,
      "winner": "left",
      "ticks": 362,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 111,
      "winner": "right",
      "ticks": 236,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 11,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 112,
      "winner": "right",
      "ticks": 312,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 2,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 113,
      "winner": "left",
      "ticks": 312,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 114,
      "winner": "left",
      "ticks": 260,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 115,
      "winner": "left",
      "ticks": 307,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 116,
      "winner": "left",
      "ticks": 352,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 6,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 117,
      "winner": "left",
      "ticks": 309,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 118,
      "winner": "left",
      "ticks": 316,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 119,
      "winner": "left",
      "ticks": 361,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 5,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 120,
      "winner": "right",
      "ticks": 261,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 12,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 121,
      "winner": "right",
      "ticks": 244,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 12,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 122,
      "winner": "left",
      "ticks": 359,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 123,
      "winner": "right",
      "ticks": 297,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 124,
      "winner": "left",
      "ticks": 261,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 10,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 125,
      "winner": "left",
      "ticks": 263,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 126,
      "winner": "right",
      "ticks": 347,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 2,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 127,
      "winner": "left",
      "ticks": 310,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 128,
      "winner": "right",
      "ticks": 236,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 12,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 129,
      "winner": "left",
      "ticks": 310,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 130,
      "winner": "left",
      "ticks": 314,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 10,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 131,
      "winner": "right",
      "ticks": 300,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 1,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 132,
      "winner": "left",
      "ticks": 262,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 10,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 133,
      "winner": "left",
      "ticks": 311,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 134,
      "winner": "right",
      "ticks": 259,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 135,
      "winner": "right",
      "ticks": 247,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 13,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 136,
      "winner": "right",
      "ticks": 244,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 15,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 137,
      "winner": "left",
      "ticks": 312,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 138,
      "winner": "left",
      "ticks": 313,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 139,
      "winner": "left",
      "ticks": 351,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 4,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 4
        }
      ]
    },
    {
      "seed": 140,
      "winner": "left",
      "ticks": 308,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 5,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 141,
      "winner": "right",
      "ticks": 276,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 6,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 142,
      "winner": "right",
      "ticks": 240,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 12,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 143,
      "winner": "left",
      "ticks": 348,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 3,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 144,
      "winner": "right",
      "ticks": 237,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 19,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 145,
      "winner": "left",
      "ticks": 263,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 10,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 146,
      "winner": "right",
      "ticks": 337,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 5,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 147,
      "winner": "left",
      "ticks": 310,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 148,
      "winner": "left",
      "ticks": 313,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 149,
      "winner": "right",
      "ticks": 262,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 150,
      "winner": "right",
      "ticks": 379,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 3,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 4
        }
      ]
    },
    {
      "seed": 151,
      "winner": "right",
      "ticks": 242,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 12,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 152,
      "winner": "left",
      "ticks": 261,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 153,
      "winner": "right",
      "ticks": 263,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 4,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 154,
      "winner": "left",
      "ticks": 309,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 155,
      "winner": "left",
      "ticks": 312,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 156,
      "winner": "left",
      "ticks": 313,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 1
        }
      ]
    },
    {
      "seed": 157,
      "winner": "left",
      "ticks": 260,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 10,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 158,
      "winner": "right",
      "ticks": 364,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 159,
      "winner": "right",
      "ticks": 261,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 160,
      "winner": "left",
      "ticks": 262,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 161,
      "winner": "left",
      "ticks": 263,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 162,
      "winner": "left",
      "ticks": 311,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 163,
      "winner": "right",
      "ticks": 266,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 164,
      "winner": "right",
      "ticks": 274,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 4,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 165,
      "winner": "right",
      "ticks": 243,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 166,
      "winner": "left",
      "ticks": 351,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 167,
      "winner": "left",
      "ticks": 307,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 168,
      "winner": "left",
      "ticks": 305,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 169,
      "winner": "right",
      "ticks": 250,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 11,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 170,
      "winner": "left",
      "ticks": 363,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 171,
      "winner": "right",
      "ticks": 236,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 14,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 172,
      "winner": "left",
      "ticks": 354,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 173,
      "winner": "left",
      "ticks": 360,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 174,
      "winner": "right",
      "ticks": 237,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 12,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 175,
      "winner": "left",
      "ticks": 271,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 176,
      "winner": "left",
      "ticks": 311,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 10,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 177,
      "winner": "left",
      "ticks": 263,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 10,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 178,
      "winner": "left",
      "ticks": 359,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 179,
      "winner": "left",
      "ticks": 357,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 6,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 180,
      "winner": "left",
      "ticks": 358,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 1
        }
      ]
    },
    {
      "seed": 181,
      "winner": "right",
      "ticks": 236,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 13,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 182,
      "winner": "left",
      "ticks": 311,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 183,
      "winner": "right",
      "ticks": 272,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 3,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 184,
      "winner": "left",
      "ticks": 312,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    },
    {
      "seed": 185,
      "winner": "left",
      "ticks": 312,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 186,
      "winner": "left",
      "ticks": 350,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 6,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 187,
      "winner": "right",
      "ticks": 231,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 17,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 188,
      "winner": "right",
      "ticks": 317,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 1,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 189,
      "winner": "right",
      "ticks": 343,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 190,
      "winner": "right",
      "ticks": 275,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 8,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 191,
      "winner": "left",
      "ticks": 306,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 1
        }
      ]
    },
    {
      "seed": 192,
      "winner": "left",
      "ticks": 260,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 193,
      "winner": "left",
      "ticks": 264,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 194,
      "winner": "right",
      "ticks": 243,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 10,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 195,
      "winner": "right",
      "ticks": 243,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 10,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 196,
      "winner": "left",
      "ticks": 358,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 197,
      "winner": "right",
      "ticks": 234,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 0
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 10,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 5
        }
      ]
    },
    {
      "seed": 198,
      "winner": "left",
      "ticks": 314,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 9,
          "hasGeneral": true,
          "generalAlive": true,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 2
        }
      ]
    },
    {
      "seed": 199,
      "winner": "left",
      "ticks": 304,
      "leftSquads": [
        {
          "squadId": "Archer1",
          "soldiers": 20,
          "survivors": 7,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 1
        }
      ],
      "rightSquads": [
        {
          "squadId": "Assassin1",
          "soldiers": 20,
          "survivors": 0,
          "hasGeneral": true,
          "generalAlive": false,
          "activations": 3
        }
      ]
    }
  ]
};
