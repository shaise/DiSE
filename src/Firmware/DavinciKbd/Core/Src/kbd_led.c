/*
 * kbd_led.c
 *
 *  Created on: Aug 18, 2021
 *      Author: shai
 *
 *      ToDo:
 */

#include "main.h"
#include "usbd_customhid.h"
#include "kbd_led.h"
#include "keycode.h"
#include "settings.h"
#include "version.h"




#define KBD_OUT_MASK (KbdO0_Pin|KbdO1_Pin|KbdO2_Pin|KbdO3_Pin|KbdO4_Pin|KbdO5_Pin|KbdO6_Pin)
#define KBD_IN_MASK  (KbdI0_Pin|KbdI1_Pin|KbdI2_Pin|KbdI3_Pin|KbdI4_Pin|KbdI5_Pin|KbdI6_Pin)
#define KBD_LED_MASK (KbdL0_Pin|KbdL1_Pin|KbdL2_Pin|KbdL3_Pin|KbdL4_Pin|KbdL5_Pin|KbdL6_Pin)
#define KBD_OUT(pin) (pin << 16)
#define KBD_RESET_MASK ((KBD_LED_MASK << 16) | KBD_OUT_MASK)
#define SHUTTLE_DEAD_ZONE 12
#define SHUTTLE_ANGLE_TICK 64
#define SHUTTLE_MAX_ANGLE_TICK (SHUTTLE_ANGLE_TICK * 3 - 1)
#define SHUTTLE_NUM_RATES 8
#define POLL_RESOLUTION 2 // 2ms poll resolution

#define RPM_REPORT_PERIOD 50 // ms
#define RPM_REPORT_TICKS (RPM_REPORT_PERIOD / POLL_RESOLUTION) // 200ms

static void HandleMessage(void);
static void SendKeyData(uint8_t keyid);
static void ReportRawKeyPresses(void);
static void SendJogData(uint8_t jogcell);
static void SendJogType(uint8_t jogmode);
static void HandleJogKeySelect(sKbdCodeData *key);
static void UpdateJogParams(int jsel);


#define DEFAULT_JOGMODE 1


#define LIFO_SIZE 32
#define LIFO_EMPTY(X) (X.head == X.tail)
typedef struct _lifo
{
	int head;
	int tail;
	uint8_t data[LIFO_SIZE];
} sLifo;

/*typedef struct
{
	uint8_t key1;
	uint8_t key2;
	uint16_t timestamp;
} kbd_debug_t;*/

#ifdef DEBUG_USB_REPORTS
#define NUM_DEBUG_RECORDS 200
int debug_count = 0;
sKbdReport_t kbd_debug[NUM_DEBUG_RECORDS];
#endif


uint32_t kbd_out_bits[KBD_COLS] = { KBD_OUT(KbdO0_Pin), KBD_OUT(KbdO1_Pin), KBD_OUT(KbdO2_Pin),
		KBD_OUT(KbdO3_Pin), KBD_OUT(KbdO4_Pin), KBD_OUT(KbdO5_Pin), KBD_OUT(KbdO6_Pin)
};

uint32_t kbd_in_bits[KBD_ROWS] = { KbdI0_Pin, KbdI1_Pin, KbdI2_Pin, KbdI3_Pin, KbdI4_Pin,
		KbdI5_Pin, KbdI6_Pin
};

uint32_t led_bits[KBD_ROWS] = { KbdL0_Pin, KbdL1_Pin, KbdL2_Pin, KbdL3_Pin,
		KbdL4_Pin, KbdL5_Pin, KbdL6_Pin
};

uint32_t kbd_vals[KBD_COLS] = {0};
sKeyState key_states[MAX_NUM_KEYS] = {0};

extern USBD_HandleTypeDef hUsbDeviceFS;
sKbdReport_t last_kbd_report = {0};
sKbdReport_t jog_report = {0};

uint8_t hid_report_buff[MAX_MSG_LEN] = {0};
uint32_t last_send_time = 0;
uint32_t last_check_time = 0;

uint8_t lastPressInGroup[MAX_GROUPS];

int16_t last_encoder = 0;
uint8_t msg_ready = 0;
uint8_t pending_msg[MAX_MSG_LEN];
uint8_t getKeyRequest = 0;
uint8_t getJogDataRequest = 0xFF;
uint8_t getJogTypeRequest = 0xFF;

int cur_jog_base = 0;
uint8_t cur_jog_mode = 0xFF;
uint8_t last_jog_mode = 0xFF;
uint8_t isShuttleMode = 0;
sJogCodeData *cur_shtl_jog_data = NULL;
int last_shtl_jogcell = -1;
int shtl_rate = 0;
int shtl_count = 0;
int jog_min_rate;
int jog_max_rate;
int shutt_rate_ix;

// keys fifo
sLifo kbd_fifo = {0};
sLifo hid_fifo = {0};
int isReportNeeded = 0;

// rpm handling
int rpmAvgCount = 0;
int16_t rpmLastVal = 0;
static int curRpm = 0;
static int lastRpm = 0;
static int curRpmAngle = 0;

// jog handling
int curJogAngleStep = 0;
int curJogAngleCount = 0;


// kbd layout:
/*
 * +---+---+---+  +---+---+---+---+  +---+-+-+---+
 * | 0 | 7 | 14|  | 21| 28| 35| 42|  |  33 |  40 |
 * +---+---+---+  +---+---+---+---+  +---+-+-+---+
 * | 1 | 8 | 15|  | 22| 29| 36| 43|  | 34| 41| 48|
 * +---+---+---+  +---+---+---+---+  +---+---+---+
 *
 * +-----+-----+  +---+---+---+---+
 * |  2  |  16 |  | 23| 30| 37| 44|
 * +---+-+-+---+  +---+---+---+---+
 * | 3 | 10| 17|  | 24| 31| 38| 45|
 * +---+---+---+  +---+---+---+---+
 * | 4 | 11| 18|  | 25| 32| 39| 46|
 * +---+---+---+  +---+---+--++---+
 * | 5 | 12| 19|  |     26   |  47|
 * +---+---+---+  +---+---+--+----+
 */

int kbdLineCount = 0;


void KbdInit(void)
{
	GPIOB->BSRR = KBD_RESET_MASK;
	last_send_time = HAL_GetTick();
	for (int i = 0; i < KBD_COLS; i++)
		kbd_vals[i] = KBD_IN_MASK;
	for (int i = 0; i < KBD_COLS; i++)
		lastPressInGroup[i] = 0xFF;
	UpdateJogParams(DEFAULT_JOGMODE);
}

void KbdCycle(void)
{
	//if (kbdLineCount == 0)
	kbd_vals[kbdLineCount] = GPIOA->IDR;
	kbdLineCount++;
	if (kbdLineCount >= KBD_COLS)
		kbdLineCount = 0;
	GPIOB->BSRR = KBD_RESET_MASK;
	GPIOB->BSRR = kbd_out_bits[kbdLineCount];
}

static int PushFifo(sLifo *fifo, uint8_t data)
{
	int newhead = (fifo->head + 1) % LIFO_SIZE;
	if (newhead == fifo->tail)
		return -1;
	fifo->data[fifo->head] = data;
	fifo->head = newhead;
	return 0;
}

static int PopFifo(sLifo *fifo)
{
	int tail = fifo->tail;
	if (tail == fifo->head)
		return -1;
	fifo->tail = (tail + 1) % LIFO_SIZE;
	return fifo->data[tail];
}

/*static int PeekFifo(sFifo *fifo)
{
	int tail = fifo->tail;
	if (tail == fifo->head)
		return -1;
	return fifo->data[tail];
}*/


#define LedOn(col, row) { kbd_out_bits[col] |= led_bits[row]; }
#define LedOff(col, row) { kbd_out_bits[col] &= ~led_bits[row]; }
#define LedToggle(col, row) { kbd_out_bits[col] ^= led_bits[row]; }
#define IsLedOn(col, row) ((kbd_out_bits[col] & led_bits[row]) != 0)

static void SendHidMessage(void)
{
	USBD_CUSTOM_HID_SendReport(&hUsbDeviceFS, CUSTOM_HID_EPIN_ADDR, hid_report_buff, MAX_MSG_LEN);
}

static void UpdateJogParams(int jsel)
{
	cur_jog_mode = (uint8_t)jsel;
	if (cur_jog_mode >= JOG_MODES)
		return;
	isShuttleMode = gSettings.jogTypes[cur_jog_mode];
	last_encoder = (int16_t)TIM1->CNT;
	cur_jog_base = cur_jog_mode * JOG_CODES_PER_MODE * 2;
}

static void HandleJogKeySelect(sKbdCodeData *key)
{
	int jsel = (key->jog_sel & JOG_SELL_MASK) - 1;
	last_jog_mode = cur_jog_mode;
	UpdateJogParams(jsel);
}

static void HandleLedGroups(int keyid, int col, int row, sKbdCodeData *key)
{
	int grp = key->group;

	if (grp == 0 || grp >= MAX_GROUPS)
		return;

	int lastLit = lastPressInGroup[grp];
	if (lastLit == keyid)
	{
		if (key->alt_type == ALT_TYPE_TOGGLE)
			LedOff(col, row);
		lastPressInGroup[grp] = 0xFF;
		return;
	}
	if (lastLit < MAX_NUM_KEYS)
		LedOff(lastLit / KBD_ROWS, lastLit % KBD_ROWS);
	LedOn(col, row);
	lastPressInGroup[grp] = keyid;
}


// the following HandleStateXxx functions are not reentrant, and global vars are used for time efficiency
sKbdCodeData *HSkey;
sKeyState *HSkeystate;
int HSisKeyPressed;
int HSi, HSj, HSkeyid;

static void HandleStatePressed(void)
{
	if (HSisKeyPressed)
	{
		if (HSkeystate->hold_counter < 0xF000)
			HSkeystate->hold_counter += POLL_RESOLUTION;

		// handle long press
		if (HSkey->alt_type == ALT_TYPE_LONGPRESS && HSkeystate->press_state == KEY_STATE_PRESSED) {
			if (HSkeystate->hold_counter > gSettings.LongPressPeriod) {
				HSkeystate->press_state = KEY_STATE_LONG_PRESS;
				PushFifo(&kbd_fifo, HSkey->alt_modifiers);
				PushFifo(&kbd_fifo, HSkey->alt_code);
			}
		}
	}
	else
	{
		if (HSkey->code != 0)
		{
			if (HSkey->alt_type == ALT_TYPE_LONGPRESS && HSkeystate->press_state == KEY_STATE_PRESSED)
			{
				PushFifo(&kbd_fifo, HSkey->modifiers);
				PushFifo(&kbd_fifo, HSkey->code);
			}
			else if (HSkey->alt_type == ALT_TYPE_KEYUP)
			{
				PushFifo(&kbd_fifo, HSkey->alt_modifiers);
				PushFifo(&kbd_fifo, HSkey->alt_code);
			}

		}
		PushFifo(&hid_fifo, HSkeyid | 0x80);
		if (HSkeystate->press_state == KEY_STATE_DBL_PRESSED)
			HSkeystate->press_state = KEY_STATE_DBL_RELEASED;
		else
			HSkeystate->press_state = KEY_STATE_RELEASED;
		if (HSkey->alt_type == ALT_TYPE_TOGGLE && (HSkey->group == 0))
			LedToggle(HSi, HSj);
		if ((HSkey->jog_sel & JOG_SELL_TEMP) != 0)
			UpdateJogParams(last_jog_mode); //  return to prev jog mode
	}
}

static void HandleStateReleased()
{
	if (HSisKeyPressed)
	{
		if (HSkey->jog_sel != 0)
			HandleJogKeySelect(HSkey);

		if (HSkeystate->press_state == KEY_STATE_DBL_RELEASED && HSkeystate->hold_counter > gSettings.DoubleClickPeriod)
			HSkeystate->press_state = KEY_STATE_RELEASED;
		if (   (HSkey->alt_type == ALT_TYPE_TOGGLE && HSkey->alt_code > 0 && IsLedOn(HSi, HSj))
			|| HSkeystate->press_state == KEY_STATE_DBL_RELEASED )
		{
			PushFifo(&kbd_fifo, HSkey->alt_modifiers);
			PushFifo(&kbd_fifo, HSkey->alt_code);
		}
		else if (HSkey->alt_type != ALT_TYPE_LONGPRESS)
		{
			PushFifo(&kbd_fifo, HSkey->modifiers);
			PushFifo(&kbd_fifo, HSkey->code);
		}
		PushFifo(&hid_fifo, HSkeyid);

		HSkeystate->hold_counter = 0;
					// handle Led states
		HandleLedGroups(HSkeyid, HSi, HSj, HSkey);

		if (HSkey->alt_type == ALT_TYPE_DOUBLE_CLICK && HSkeystate->press_state != KEY_STATE_DBL_RELEASED)
			HSkeystate->press_state = KEY_STATE_DBL_PRESSED;
		else
			HSkeystate->press_state = KEY_STATE_PRESSED;
	}
	else
	{
		if (HSkey->alt_type == ALT_TYPE_DOUBLE_CLICK)
		{
			if (HSkeystate->hold_counter < 0xFFFF)
				HSkeystate->hold_counter += POLL_RESOLUTION;
		}

	}
}



static void ScanPressedKeys()
{

	for (HSi = 0; HSi < KBD_COLS; HSi++)
	{
		int val = kbd_vals[HSi] & KBD_IN_MASK;
		//if (val == KBD_IN_MASK)
		//	continue;
		for (HSj = 0; HSj < KBD_ROWS; HSj++)
		{
			HSkeyid = HSi * KBD_ROWS + HSj;
			HSkey = &(gSettings.codeTable[HSkeyid]);
			HSkeystate = &(key_states[HSkeyid]);
			HSisKeyPressed = (val & kbd_in_bits[HSj]) == 0;
			if (HSisKeyPressed)
				HSisKeyPressed = 1;

			switch (HSkeystate->press_state)
			{
			case KEY_STATE_LONG_PRESS:
			case KEY_STATE_DBL_PRESSED:
			case KEY_STATE_PRESSED: HandleStatePressed(); break;
			case KEY_STATE_DBL_RELEASED:
			case KEY_STATE_RELEASED: HandleStateReleased(); break;
			}
		}
	}
}

static int CalcShuttleRateParams(int cntdiff, int dir)
{
	static int rate;
	if (cntdiff > SHUTTLE_MAX_ANGLE_TICK)
		cntdiff = SHUTTLE_MAX_ANGLE_TICK;
	int type = cntdiff / SHUTTLE_ANGLE_TICK;
	cntdiff %= SHUTTLE_ANGLE_TICK;
	int jogcell = cur_jog_base + type * 2 + dir;
	if (jogcell != last_shtl_jogcell)
	{
		last_shtl_jogcell = jogcell;
		sJogCodeData *jd = &(gSettings.jogTable[jogcell]);
		if (jd->code != 0)
		{
			cur_shtl_jog_data = jd;
			if (jd->rate1 > 0)
				jog_min_rate = (1000 / POLL_RESOLUTION) / jd->rate1;
			else
				jog_min_rate = 100;
			if (jd->rate2 > 0)
				jog_max_rate = (1000 / POLL_RESOLUTION) / jd->rate2;
			else
				jog_max_rate = 0;
			if (jog_max_rate > jog_min_rate)
				jog_max_rate = jog_min_rate;
		}
		shutt_rate_ix = -1;
	}
	int new_shutt_rate_ix = (cntdiff * SHUTTLE_NUM_RATES) / SHUTTLE_ANGLE_TICK;
	if (new_shutt_rate_ix != shutt_rate_ix)
	{
		int ratediff = jog_min_rate - jog_max_rate;
		shutt_rate_ix = new_shutt_rate_ix;
		rate = jog_min_rate - shutt_rate_ix * ratediff / (SHUTTLE_NUM_RATES - 1);
	}
	return rate;
}

static int ShuttleJog(int cntdiff, int dir)
{
	if (cntdiff < SHUTTLE_DEAD_ZONE)
		return 0;
	cntdiff -= SHUTTLE_DEAD_ZONE;
	shtl_rate = CalcShuttleRateParams(cntdiff, dir);
	if (cur_shtl_jog_data == NULL)
		return 0;
	shtl_count++;
	if (shtl_count > shtl_rate)
	{
		shtl_count = 0;
		jog_report.modifiers = cur_shtl_jog_data->modifiers;
		jog_report.keys[0] = cur_shtl_jog_data->code;
		USBD_CUSTOM_HID_SendReport(&hUsbDeviceFS, CUSTOM_KBD_EPIN_ADDR, (uint8_t *)&jog_report, sizeof(sKbdReport_t));
		return 1;
	}
	return 0;
}

static int StandardJog(int rpm, int cntdiff, int dir)
{
	sJogCodeData *jd;
	int i;
	if (rpm == 0)
		return 0;
	if (rpm > 250)
		rpm = 250;
	for (i = 0; i < JOG_CODES_PER_MODE; i++)
	{
		jd = &(gSettings.jogTable[cur_jog_base + i * 2 + dir]);
		if (jd->code == 0)
			return 0;
		if (rpm <= jd->rate1)
			break;
	}
	if (i >= JOG_CODES_PER_MODE)
		return 0;

	curJogAngleStep = jd->rate2;
	if (curJogAngleStep == 0)
		curJogAngleStep = 5;
	curJogAngleCount += cntdiff;
	if (curJogAngleCount < curJogAngleStep)
		return 0;
	curJogAngleCount = 0;
	jog_report.modifiers = jd->modifiers;
	jog_report.keys[0] = jd->code;
	USBD_CUSTOM_HID_SendReport(&hUsbDeviceFS, CUSTOM_KBD_EPIN_ADDR, (uint8_t *)&jog_report, sizeof(sKbdReport_t));
	return 1;
}

int PollJogWheel(void)
{
	int16_t cnt = (int16_t)TIM1->CNT;
	int16_t cntdiff;
	rpmAvgCount++;

	if (rpmAvgCount > RPM_REPORT_TICKS)
	{
		cntdiff = cnt - rpmLastVal;
		if (cntdiff < 0)
			cntdiff = -cntdiff;

		//curRpm = cntdiff * 1000 * 60 / (400 * RPM_REPORT_PERIOD);
		curRpm = cntdiff;
		rpmAvgCount = 0;
		rpmLastVal = cnt;
		curRpmAngle = ((cnt % 400) * 360 / 400);
	}

	if (USBD_HID_Busy(&hUsbDeviceFS, CUSTOM_KBD_EPIN_ADDR))
		return 0;

	if (cur_jog_mode >= JOG_MODES)
	{
		last_encoder = cnt;
		return 0;
	}

	// release last key if needed
	if (jog_report.keys[0] != 0)
	{
		jog_report.modifiers = 0;
		jog_report.keys[0] = 0;
		USBD_CUSTOM_HID_SendReport(&hUsbDeviceFS, CUSTOM_KBD_EPIN_ADDR, (uint8_t *)&jog_report, sizeof(sKbdReport_t));
		return 1;
	}

	cntdiff = cnt - last_encoder;
	int dir = 0;
	if (cntdiff < 0)
	{
		dir = 1;
		cntdiff = - cntdiff;
	}

	int res = 0;
	if (isShuttleMode)
		res = ShuttleJog(cntdiff, dir);
	else
	{
		res = StandardJog(curRpm, cntdiff, dir);
		last_encoder = cnt;
	}
	return res;
}



// return true when device is configured and sufficient time passed after
int IsDeviceReady(void)
{
	uint32_t curtime = HAL_GetTick();
	if (!USBD_HID_Attached(&hUsbDeviceFS))
	{
		last_check_time = curtime;
		return 0;
	}
	if ((curtime - last_check_time) > 1000)
		return 1;
	return 0;
}


static void PollKeyPresses(void)
{
	sKbdReport_t treport = {0};

	ScanPressedKeys();

	if (!USBD_HID_Busy(&hUsbDeviceFS, CUSTOM_KBD_EPIN_ADDR))
	{

		// Fixme: possible improvement: if few keys in fifo share same modifiers - add to the same report
		if (!LIFO_EMPTY(kbd_fifo))
		{
			treport.modifiers = PopFifo(&kbd_fifo);
			treport.keys[0] = PopFifo(&kbd_fifo);
			isReportNeeded = 1;
		}

#ifdef DEBUG_USB_REPORTS
		if (debug_count < NUM_DEBUG_RECORDS)
			kbd_debug[debug_count++] = treport;
#endif
		if (isReportNeeded)
		{
			last_kbd_report = treport;
			USBD_CUSTOM_HID_SendReport(&hUsbDeviceFS, CUSTOM_KBD_EPIN_ADDR, (uint8_t *)&last_kbd_report, sizeof(sKbdReport_t));
			if (treport.keys[0] == 0 && treport.modifiers == 0)
				isReportNeeded = 0;
		}
	}
}

static void PollHidComm(void)
{
	if (!USBD_HID_Busy(&hUsbDeviceFS, CUSTOM_HID_EPIN_ADDR))
	{
		if (getKeyRequest > 0) {
			SendKeyData(getKeyRequest);
			getKeyRequest = 0;
		} else if (getJogDataRequest != 0xFF) {
			SendJogData(getJogDataRequest);
			getJogDataRequest = 0xFF;
		} else if (getJogTypeRequest != 0xFF) {
			SendJogType(getJogTypeRequest);
			getJogTypeRequest = 0xFF;
		}
		else
			ReportRawKeyPresses();
	}
}

void PollKbdJog(void)
{
	if (!IsDeviceReady())
		return;

	if (msg_ready)
		HandleMessage();

	uint32_t curtime = HAL_GetTick();
	uint32_t timelapse = curtime - last_send_time;
	if (timelapse < POLL_RESOLUTION)
		return;
	last_send_time = curtime;

	PollJogWheel();
	PollKeyPresses();
	PollHidComm();
}

static void SendKeyData(uint8_t keyid)
{
	if (keyid > MAX_NUM_KEYS)
		return;
	sMsgKeyData *msg = (sMsgKeyData *)hid_report_buff;
	sKbdCodeData *key = &(gSettings.codeTable[keyid - 1]);
	msg->cmd = MSG_KEY_DATA;
	msg->datalen = 8;
	msg->keyid = keyid;
	msg->keycode = key->code;
	msg->modifiers = key->modifiers;
	msg->group = key->group;
	msg->alt_type = key->alt_type;
	msg->alt_code = key->alt_code;
	msg->alt_modifiers = key->alt_modifiers;
	msg->jog_sel = key->jog_sel;
	SendHidMessage();
}

static void SendJogData(uint8_t jogcell)
{
	if (jogcell >= MAX_JOG_CODES)
		return;
	sMsgJogData *msg = (sMsgJogData *)hid_report_buff;
	sJogCodeData *jogdata = &(gSettings.jogTable[jogcell]);
	msg->cmd = MSG_JOG_DATA;
	msg->datalen = 7;
	msg->dir = jogcell % 2;
	jogcell /= 2;
	msg->rate_select = jogcell % JOG_CODES_PER_MODE;
	jogcell /= JOG_CODES_PER_MODE;
	msg->mode = jogcell;
	msg->keycode = jogdata->code;
	msg->modifiers = jogdata->modifiers;
	msg->rate1 = jogdata->rate1;
	msg->rate2 = jogdata->rate2;
	SendHidMessage();
}
static void SendJogType(uint8_t jogmode)
{
	if (jogmode >= JOG_MODES)
		return;
	sMsgJogType *msg = (sMsgJogType *)hid_report_buff;
	msg->cmd = MSG_JOG_TYPE;
	msg->datalen = 2;
	msg->mode = jogmode;
	msg->type = gSettings.jogTypes[jogmode];
	SendHidMessage();
}

static void ReportRawKeyPresses()
{
	// report raw key states
	int npressed = 0;
	int nreleased = 0;
	sMsgKeyActions *msg = (sMsgKeyActions *)hid_report_buff;

	while (npressed < MAX_KEY_ACTIONS && nreleased < MAX_KEY_ACTIONS)
	{
		int code = PopFifo(&hid_fifo);
		if (code < 0)
			break;
		int isPressed = (code & 0x80) == 0;
		code = (code & ~0x80) + 1;
		if (isPressed && npressed < MAX_KEY_ACTIONS)
		{
			msg->pressed[npressed] = code;
			npressed++;
		}
		else if (!isPressed && nreleased < MAX_KEY_ACTIONS)
		{
			msg->released[nreleased] = code;
			nreleased++;
		}
	}

	if (npressed > 0 || nreleased > 0 || curRpm != lastRpm)
	{
		msg->cmd = MSG_KEY_ACTIONS;
		msg->datalen = 2 * MAX_KEY_ACTIONS;
		if (npressed < MAX_KEY_ACTIONS)
			msg->pressed[npressed] = 0;
		if (nreleased < MAX_KEY_ACTIONS)
			msg->released[nreleased] = 0;
		msg->jog_rpm = curRpm;
		msg->jog_angle = curRpmAngle;
		lastRpm = curRpm;
		SendHidMessage();
	}

}

void ConfigMsgReceived(uint8_t *buff)
{
	if (msg_ready)
		return; // prev msg not handled yet
	memcpy(pending_msg, buff, MAX_MSG_LEN);
	msg_ready = 1;
}

static void UpdateKey(void)
{
	sMsgKeyData *msg = (sMsgKeyData *)pending_msg;
	int keyid = msg->keyid - 1;
	if (keyid >= 0 && keyid < MAX_NUM_KEYS)
	{
		sKbdCodeData *key = &(gSettings.codeTable[keyid]);
		key->code = msg->keycode;
		key->modifiers = msg->modifiers;
		key->group = msg->group;
		key->alt_type = msg->alt_type;
		key->alt_code = msg->alt_code;
		key->alt_modifiers = msg->alt_modifiers;
		key->jog_sel = msg->jog_sel;
	}
}

static uint8_t getJogDataCell(uint8_t mode, uint8_t rate, uint8_t dir)
{
	return (mode * JOG_CODES_PER_MODE + rate) * 2 + dir;
}

static void SetJogData(void)
{
	sMsgJogData *msg = (sMsgJogData *)pending_msg;
	int jogcell = getJogDataCell(msg->mode, msg->rate_select, msg->dir);
	if (jogcell >= MAX_JOG_CODES)
		return;
	sJogCodeData * jogdata = (sJogCodeData *)&gSettings.jogTable[jogcell];
	jogdata->code = msg->keycode;
	jogdata->modifiers = msg->modifiers;
	jogdata->rate1 = msg->rate1;
	jogdata->rate2 = msg->rate2;
}

static void SetJogType(void)
{
	sMsgJogType *msg = (sMsgJogType *)pending_msg;
	uint8_t jogmode = msg->mode;
	uint8_t type = msg->type;
	if (jogmode >= JOG_MODES || type > 1)
		return;
	gSettings.jogTypes[jogmode] = type;
	if (jogmode == cur_jog_mode)
		isShuttleMode = type;
}




static void GetKey(void)
{
	sMsgGetKey *msg = (sMsgGetKey *)pending_msg;
	getKeyRequest = msg->keyid;
}

static void HandleSaveSettings(void)
{
	sMsgSaveResults *msg = (sMsgSaveResults *)hid_report_buff;
	msg->cmd = MSG_SAVE_RESULT;
	msg->datalen = 1;
	msg->saveres = (uint8_t)SaveSettings();
	SendHidMessage();
}

static void HandleFactoryDefault(void)
{
	sMsgSaveResults *msg = (sMsgSaveResults *)hid_report_buff;
	msg->cmd = MSG_SAVE_RESULT;
	msg->datalen = 1;
	msg->saveres = (uint8_t)FactoryDefault();
	SendHidMessage();
}

static void GetJogData(void)
{
	sMsgGetJogData *msg = (sMsgGetJogData *)pending_msg;
	getJogDataRequest = getJogDataCell(msg->mode, msg->rate, msg->dir);
}

static void GetJogType(void)
{
	sMsgGetJogType *msg = (sMsgGetJogType *)pending_msg;
	getJogTypeRequest = msg->mode;
}

static void HandleGetVersion(void)
{
	sMsgVersion *msg = (sMsgVersion *)hid_report_buff;
	msg->cmd = MSG_VERSION;
	msg->datalen = 2;
	msg->ver_major = VER_MAJOR;
	msg->ver_minor = VER_MINOR;
	SendHidMessage();
}



static void HandleMessage(void)
{
	uint8_t cmd = pending_msg[0];

	switch (cmd)
	{
	case MSG_SET_KEY:          UpdateKey(); break;
	case MSG_SAVE_SETTINGS:    HandleSaveSettings(); break;
	case MSG_GET_KEY:          GetKey(); break;
	case MSG_SET_JOG_DATA:     SetJogData(); break;
	case MSG_SET_JOG_TYPE:     SetJogType(); break;
	case MSG_GET_JOG_DATA:     GetJogData(); break;
	case MSG_GET_JOG_TYPE:     GetJogType(); break;
	case MSG_GET_VERSION:      HandleGetVersion(); break;
	case MSG_FACTORY_DEFAULT:  HandleFactoryDefault(); break;
	}


	msg_ready = 0;
}
