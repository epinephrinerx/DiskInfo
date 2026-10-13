/*---------------------------------------------------------------------------*/
// DiskInfoCore C ABI. Contract: docs/CORE_ABI.md. Keep this header plain C:
// no C++ types, no MFC, so P/Invoke and a future portable core can share it.
/*---------------------------------------------------------------------------*/

#ifndef DISKINFO_CORE_API_H
#define DISKINFO_CORE_API_H

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/* "No value". CAtaSmart itself uses -1000 (temperature) and -1 (life, hours). */
#define DI_NA   ((int32_t)INT32_MIN)
#define DI_NA64 ((int64_t)INT64_MIN)

#define DI_ABI_MAJOR 1
#define DI_ABI_MINOR 0

/* error codes */
#define DI_OK              0
#define DI_E_ARG          (-1)
#define DI_E_ACCESS       (-2)  /* needs administrator; partial results may exist */
#define DI_E_NOT_FOUND    (-3)
#define DI_E_IO           (-4)
#define DI_E_UNSUPPORTED  (-5)
#define DI_E_STRUCT_SIZE  (-6)

/* di_open flags */
#define DI_OPEN_NO_WMI  0x00000001u  /* skip WMI/COM disk search */

/* interface_kind: CAtaSmart::INTERFACE_TYPE values 0..6; its AMD_RC2 (7) and anything else is DI_IF_OTHER */
#define DI_IF_UNKNOWN  0
#define DI_IF_PATA     1
#define DI_IF_SATA     2
#define DI_IF_USB      3
#define DI_IF_IEEE1394 4
#define DI_IF_SCSI     5
#define DI_IF_NVME     6
#define DI_IF_OTHER    7

/* media */
#define DI_MEDIA_UNKNOWN 0
#define DI_MEDIA_HDD     1
#define DI_MEDIA_SSD     2

/* health: same numbering as CAtaSmart::DISK_STATUS */
#define DI_HEALTH_UNKNOWN 0
#define DI_HEALTH_GOOD    1
#define DI_HEALTH_CAUTION 2
#define DI_HEALTH_BAD     3

/* access */
#define DI_ACCESS_FULL    0u  /* SMART data read and checksum-correct */
#define DI_ACCESS_PARTIAL 1u  /* disk listed, SMART data missing or not trusted */

typedef struct di_handle di_handle;

typedef struct {
  uint32_t struct_size;         /* caller sets to its sizeof (>= this v1 size); fields may be appended later */
  int32_t  physical_drive_id;   /* N of \\.\PhysicalDriveN */
  char     model[64];           /* UTF-8, NUL-terminated, truncated if longer */
  char     serial[32];
  char     firmware[16];
  int32_t  interface_kind;      /* DI_IF_* */
  int32_t  media;               /* DI_MEDIA_* */
  int32_t  temperature_c;       /* DI_NA if none */
  int32_t  health;              /* DI_HEALTH_* */
  int32_t  life_percent;        /* DI_NA if none */
  int64_t  power_on_hours;      /* DI_NA64 if none */
  uint32_t access;              /* DI_ACCESS_* */
  uint32_t source;              /* CAtaSmart::COMMAND_TYPE value used to read the disk */
} di_disk_info_t;

uint32_t di_abi_version(void);               /* major << 16 | minor */

int  di_open(di_handle** out, uint32_t flags);
void di_close(di_handle* h);

/* Slow (seconds): enumerates every disk. Never call from a UI thread.
   Initializes COM on the calling thread itself. One thread per handle.
   Not isolated between handles: the scan calls CoInitializeSecurity (process-wide), may start
   the WMI service and uses file-level state, so serialize scans across the whole process.
   Returns DI_E_ACCESS when not elevated and no disk was found (needs administrator). */
int  di_scan(di_handle* h);
int  di_disk_count(const di_handle* h);      /* >= 0, or a negative DI_E_* */
int  di_disk_info(const di_handle* h, int idx, di_disk_info_t* out);

#ifdef __cplusplus
}
#endif

#endif /* DISKINFO_CORE_API_H */
