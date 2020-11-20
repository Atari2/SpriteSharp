;Basically the same as %SpawnExtended(), except this spawns minor extended sprite.
;Yeah, I'm good at making new shared routines

;Input:  A   = sprite number
;        $00 = x offset  \
;        $01 = y offset  | you could also just ignore these and set them later
;        $02 = x speed   | using the returned Y.
;        $03 = y speed   /
;
;Output: Y   = index to extended sprite (#$FF means no sprite spawned)
;        C   = Carry Set = spawn failed, Carry Clear = spawn successful.

	XBA			; Save A
	LDA !15A0,x		; \
	ORA !186C,x		; | return if offscreen or about to be eaten
	ORA !15D0,x		; |
	BNE .ret		; /

	LDY #$0B		; go and check all minor extended sprites
.loop
	LDA $17F0|!Base2,y	; \ if empty, proceed
	BEQ .FoundSlot		; /
	DEY			; \
	BPL .loop		; | if not, decrease Y and continue with loop
	SEC			; | set carry if none is spawned
	RTL			; /

.FoundSlot
	XBA			; get number back in A
	STA $17F0|!Base2,y 	;
	
	LDA $00			; \
	CLC : ADC !E4,x		; |
	STA $1808|!Base2,y	; | store x position + x offset (low byte)

	LDA #$00		; |
	BIT $00			; | create high byte based on $00 in A and add
	BPL ?+			; | to x position
	DEC			; |
?+	ADC !14E0,x		; |
	STA $18EA|!Base2,y	; /
		
	LDA $01			; \ 
 	CLC : ADC !D8,x		; |
	STA $17FC|!Base2,y	; | store y position + y offset

	LDA #$00		; |
	BIT $01			; | create high byte based on $01 in A and add
	BPL ?+			; | to y position
	DEC			; |
?+	ADC !14D4,x		; |
	STA $1814|!Base2,y	; /
	
	LDA $02			; \ store x speed
	STA $182C|!Base2,y	; /

	LDA $03			; \ store y speed
	STA $1820|!Base2,y	; /
	
.ret
	CLC
	RTL