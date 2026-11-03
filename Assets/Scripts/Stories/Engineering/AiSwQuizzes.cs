// 컴퓨터공학과 problems. Majors type the missing code. Non-majors get the same bug as an easier, plain-language multiple choice.
public static class AiSwQuizzes
{
    public static Quiz Find(int chapter, int stage, bool experienced)
    {
        if (stage != 0) return null;
        switch (chapter) {
            case 3: return experienced ? LoopMajor : LoopMinor;
            case 4: return experienced ? SearchMajor : SearchMinor;
            case 6: return experienced ? EmptyMajor : EmptyMinor;
            default: return null;
        }
    }

    // Chapter 3: first lab, the loop never ends.
    static readonly Quiz LoopMajor = new Quiz {
        Prompt = "for (int i = 0; i < 10; ____)   // 끝나지 않던 반복문, 빈칸에 들어갈 코드는?",
        Accepted = new[] { "i++", "++i", "i+=1", "i=i+1" },
        Replies = new[] {
            new[] { "아닙니다. i가 줄어들면 i < 10은 영원히 참입니다. 정답은 i++, 값을 늘려야 종료 조건에 도달합니다.", "맞습니다. 증가시켜야 종료 조건에 도달합니다. 문법이 아니라 실행 흐름을 읽은 겁니다." },
            new[] { "아쉬워요! 숫자가 계속 뒤로 가고 있었어요. 정답은 i++, 한 칸씩 앞으로 보내야 10에서 퇴근해요.", "맞아요! 10으로 가야 하는데 계속 뒤로 갔네요. 이제 숫자가 제대로 퇴근하겠어요." }
        }
    };
    static readonly Quiz LoopMinor = new Quiz {
        Prompt = "0부터 10까지 세다 멈춰야 하는데 숫자가 0, -1, -2…로 줄어든다. 어떻게 고칠까?",
        Options = new[] { "숫자를 매번 0으로 되돌린다.", "숫자를 1씩 늘리도록 바꾼다.", "멈출 숫자를 100으로 바꾼다." },
        Answer = 1,
        Replies = new[] {
            new[] { "0으로 되돌리면 숫자가 10에 영영 닿지 못합니다. 1씩 늘려야 멈춥니다.", "맞습니다. 숫자가 늘어나야 10에 도달해 반복이 끝납니다. 실행 흐름을 제대로 읽었군요.", "멈출 숫자를 바꿔도 숫자가 줄어들면 도달하지 못합니다. 늘리는 방향으로 고쳐야 합니다." },
            new[] { "계속 0이면 숫자가 출발선에 갇히겠죠? 한 칸씩 앞으로 보내 봐요.", "맞아요! 숫자가 거꾸로 달리고 있었네요. 이제 10에서 제대로 퇴근하겠어요.", "목적지만 100으로 바꾸면 더 멀어지겠네요. 숫자가 커지는 방향으로 바꿔 봅시다!" }
        }
    };

    // Chapter 4: office hour, the search returns an empty list.
    static readonly Quiz SearchMajor = new Quiz {
        Prompt = "if (__room.Booked) free.Add(room);   // 빈 강의실만 담으려면 빈칸에 들어갈 기호는?",
        Accepted = new[] { "!", "!room.booked" },
        Replies = new[] {
            new[] { "아닙니다. 지금 조건은 예약된 강의실을 고릅니다. 정답은 !, 조건을 뒤집어야 합니다.", "맞습니다. 조건이 반대라 예약된 강의실만 골랐고, 예약 없는 시간표에서는 결과가 비었습니다." },
            new[] { "아깝다! 범인은 조건문이에요. 느낌표 !를 붙여서 예약 안 된 강의실을 골라야 해요.", "정답! 빈 강의실 찾기가 예약 강의실 찾기가 되어 있었네요. 느낌표 하나로 해결!" }
        }
    };
    static readonly Quiz SearchMinor = new Quiz {
        Prompt = "빈 강의실을 찾는 프로그램이 '예약된' 강의실만 골라 결과가 비었다. 어떻게 고칠까?",
        Options = new[] { "결과를 담는 목록을 새로 만든다.", "테스트 시간표에 예약을 일부러 추가한다.", "'예약 안 된' 강의실을 고르도록 조건을 뒤집는다." },
        Answer = 2,
        Replies = new[] {
            new[] { "목록은 원인이 아닙니다. 프로그램이 무엇을 고르는지 확인하세요. 조건을 뒤집어야 합니다.", "입력을 바꿔 결과를 맞추면 버그를 숨기는 겁니다. 고르는 조건을 고치세요.", "맞습니다. 조건이 반대라 예약된 강의실만 골랐습니다. 원인을 정확히 짚었군요." },
            new[] { "목록은 잘 만들어져요. 범인은 고르는 조건! 예약 안 된 강의실을 골라야죠?", "앗, 테스트를 버그에 맞추면 안 돼요! 조건을 뒤집어 봅시다.", "정답! 빈 강의실 찾기가 예약 강의실 찾기가 되어 있었네요. 조건 하나로 해결!" }
        }
    };

    // Chapter 6: the night before, an empty result looks like a crash.
    static readonly Quiz EmptyMajor = new Quiz {
        Prompt = "if (free.____ == 0) Show(\"빈 강의실이 없습니다\");   // List의 개수를 확인하는 속성은?",
        Accepted = new[] { "count", "free.count" },
        Replies = new[] {
            new[] { "아닙니다. List의 개수는 Count로 확인합니다. 비었을 때를 따로 처리해야 사용자가 상황을 이해합니다.", "맞습니다. 결과가 없다는 사실도 출력해야 사용자가 상황을 이해합니다. 수정 후 같은 입력으로 다시 테스트하세요." },
            new[] { "아쉬워요! 목록 개수는 Count예요. 0개일 때 안내 문구를 띄워 주면 사용자도 안심!", "정답! 이제 빈 화면 대신 '빈 강의실이 없습니다'가 뜨겠네요. 사용자도 안심!" }
        }
    };
    static readonly Quiz EmptyMinor = new Quiz {
        Prompt = "빈 강의실이 하나도 없을 때 화면이 텅 비어 고장처럼 보인다. 어떻게 고칠까?",
        Options = new[] { "'빈 강의실이 없습니다' 안내 문구를 띄운다.", "아무 강의실이나 하나 골라 보여 준다.", "결과가 없으면 프로그램을 종료한다." },
        Answer = 0,
        Replies = new[] {
            new[] { "맞습니다. 결과가 없다는 사실도 알려 줘야 사용자가 상황을 이해합니다. 고친 뒤 다시 테스트하세요.", "없는 결과를 지어내면 잘못된 정보를 주는 겁니다. 비어 있을 때는 안내 문구를 보여 주세요.", "꺼져 버리면 더 고장처럼 보입니다. 결과가 없다는 안내를 보여 주세요." },
            new[] { "정답! 이제 빈 화면 대신 안내 문구가 뜨겠네요. 사용자도 안심!", "304호가 갑자기 늘 비어 있으면 304호 수업 듣는 사람들이 놀라겠죠? 안내 문구로 가요!", "앱이 꺼지면 사용자가 더 당황해요. 비었다고 친절하게 알려 줍시다!" }
        }
    };
}
