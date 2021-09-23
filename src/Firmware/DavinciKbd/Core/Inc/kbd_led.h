/*
 * kbd_led.h
 *
 *  Created on: Aug 18, 2021
 *      Author: shai
 */

#ifndef INC_KBD_LED_H_
#define INC_KBD_LED_H_

#include "stm32f1xx_hal.h"

#define KBD_COLS 7
#define KBD_ROWS 7
#define MAX_NUM_KEYS (KBD_COLS * KBD_ROWS)

#define JOG_MODES 6
#define JOG_CODES_PER_MODE 3
#define MAX_JOG_CODES (JOG_MODES * JOG_CODES_PER_MODE * 2) // 2 is for left / right
#define GROUP_JOG_SELECT 50
#define MAX_GROUPS (GROUP_JOG_SELECT + 1)

#define MAX_REPORT_KEYS 6
typedef struct sKbdReport
{
	uint8_t modifiers;
	uint8_t reserved;
	uint8_t keys[MAX_REPORT_KEYS];
} sKbdReport_t;

typedef struct _sKeyState
{
	uint8_t press_state;
	uint8_t flags;
	uint16_t hold_counter;
} sKeyState;

void KbdInit(void);
void KbdCycle(void);
void LedOn(int keyid);
void LedOff(int keyid);
int GetPressedKeys(sKbdReport_t *kbdrep);
void PollKbdJog(void);
void ConfigMsgReceived(uint8_t *buff);


#endif /* INC_KBD_LED_H_ */
