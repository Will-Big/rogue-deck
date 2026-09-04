# 규칙 4 검사 — public 인스턴스 필드를 찾는다.
#
# grep으로는 못 하는 것이 하나 있어 awk를 쓴다: 규칙 4의 예외인 **중첩 [Serializable] DTO**는
# 건너뛰어야 하는데, 그 판정에는 줄 하나가 아니라 중괄호 깊이라는 문맥이 필요하다.
#
# 위반이 아닌 것: public 프로퍼티(=>), const, static, event, 그리고 [Serializable] 타입의 본문.
# 사용: awk -f Tools/lint-public-fields.awk <파일...>

FNR == 1 { depth = 0; pending = 0; dto = 0; dtodepth = 0; opened = 0 }

{
    line = $0
    comment = (line ~ /^[[:space:]]*(\/\/|\*|\/\*)/)

    # [Serializable] 다음에 오는 첫 타입 선언부터 DTO 본문으로 본다.
    if (!comment && line ~ /\[Serializable\]/) {
        pending = 1
    } else if (pending && line ~ /(struct|class)[[:space:]]+[A-Za-z_]/) {
        dto = 1; dtodepth = depth; opened = 0; pending = 0
    }

    if (!comment && !dto &&
        line ~ /^[[:space:]]*public[[:space:]]+[A-Za-z_][A-Za-z0-9_<>,.[:space:]]*[[:space:]]+[A-Za-z_][A-Za-z0-9_]*[[:space:]]*[=;]/ &&
        line !~ /=>/ &&
        line !~ /[[:space:]](const|static|event|delegate|class|struct|enum)[[:space:]]/) {
        printf "%s:%d:%s\n", FILENAME, FNR, $0
    }

    opens = gsub(/{/, "{", line)
    closes = gsub(/}/, "}", line)
    depth += opens - closes
    if (dto) {
        if (depth > dtodepth) opened = 1
        else if (opened) dto = 0
    }
}
