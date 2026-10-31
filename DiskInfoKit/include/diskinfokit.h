/*---------------------------------------------------------------------------*/
// DiskInfoKit C API, version 1.0. Contract: docs/DISKINFOKIT_API.md.
// Plain C99 header: <stdint.h>/<stddef.h> only, no Win32 types, no MFC, no C++.
// Calling convention: cdecl (DIK_CALL). All text buffers are UTF-8, NUL-terminated.
// Windows: the host .exe must declare Windows 10 in its manifest (<supportedOS> {8e0f7a12-...})
// or CAtaSmart sees Windows 8 via VerifyVersionInfo and skips NVMe disks (AtaSmart.cpp:44).
/*---------------------------------------------------------------------------*/

#ifndef DISKINFO_KIT_H
#define DISKINFO_KIT_H

#include <stdint.h>
#include <stddef.h>

#if defined(_WIN32)
#define DIK_CALL __cdecl
#else
#define DIK_CALL
#endif

#ifdef __cplusplus
extern "C" {
#endif

#define DIK_API_MAJOR 1
#define DIK_API_MINOR 0
#define DIK_NA   ((int32_t)INT32_MIN)
#define DIK_NA64 ((int64_t)INT64_MIN)

/* errors: same values as DI_* in DiskInfoCore's core_api.h */
#define DIK_OK 0
#define DIK_E_ARG (-1)
#define DIK_E_ACCESS (-2)
#define DIK_E_NOT_FOUND (-3)
#define DIK_E_IO (-4)
#define DIK_E_UNSUPPORTED (-5)
#define DIK_E_STRUCT_SIZE (-6)

#define DIK_OPEN_NO_WMI 0x00000001u

/* interface: same numbering as DI_IF_* in DiskInfoCore's core_api.h */
#define DIK_IF_UNKNOWN  0
#define DIK_IF_PATA     1
#define DIK_IF_SATA     2
#define DIK_IF_USB      3
#define DIK_IF_IEEE1394 4
#define DIK_IF_SCSI     5
#define DIK_IF_NVME     6
#define DIK_IF_OTHER    7

/* media */
#define DIK_MEDIA_UNKNOWN 0
#define DIK_MEDIA_HDD     1
#define DIK_MEDIA_SSD     2

/* health: same as DI_HEALTH_* in DiskInfoCore's core_api.h */
#define DIK_HEALTH_UNKNOWN 0
#define DIK_HEALTH_GOOD    1
#define DIK_HEALTH_CAUTION 2
#define DIK_HEALTH_BAD     3

/* access */
#define DIK_ACCESS_FULL    0u
#define DIK_ACCESS_PARTIAL 1u

/* features bitmask */
#define DIK_FEAT_SMART      0x0001u  /* IsSmartSupported */
#define DIK_FEAT_APM        0x0002u
#define DIK_FEAT_AAM        0x0004u
#define DIK_FEAT_NCQ        0x0008u
#define DIK_FEAT_TRIM       0x0010u
#define DIK_FEAT_DEVSLEEP   0x0020u
#define DIK_FEAT_STREAMING  0x0040u
#define DIK_FEAT_GPL        0x0080u
#define DIK_FEAT_VWC        0x0100u  /* IsVolatileWriteCachePresent */
#define DIK_FEAT_LBA48      0x0200u
#define DIK_FEAT_NVCACHE    0x0400u
#define DIK_FEAT_APM_ON     0x0800u  /* IsApmEnabled */
#define DIK_FEAT_AAM_ON     0x1000u

/* attribute status: dicore::AttributeStatus */
#define DIK_ATTR_STATUS_UNKNOWN 0
#define DIK_ATTR_STATUS_GOOD    1
#define DIK_ATTR_STATUS_CAUTION 2
#define DIK_ATTR_STATUS_BAD     3

typedef struct dik_handle dik_handle;

/* Caller sets struct_size to sizeof(dik_disk_t) (must be >= v1 size).
   The DLL rejects smaller structs with DIK_E_STRUCT_SIZE and writes only its own size. */
typedef struct {
  uint32_t struct_size;
  int32_t  physical_drive_id;   /* N of \\.\PhysicalDriveN on Windows; backend-defined index elsewhere */
  char     model[96];
  char     serial[48];
  char     firmware[24];
  char     interface_name[32];  /* CAtaSmart::Interface, e.g. "Serial ATA", "NVM Express" */
  char     transfer_current[32];/* CurrentTransferMode */
  char     transfer_max[32];    /* MaxTransferMode */
  char     standard[64];        /* MajorVersion (+ " | " + MinorVersion if not empty), as the GUI shows it */
  char     drive_letters[32];   /* DriveMap; empty off Windows */
  char     smart_key[32];       /* SmartKeyName, e.g. "Smart", "SmartNVMe": selects the attribute-name table */
  int32_t  interface_kind;      /* DIK_IF_* */
  int32_t  media;               /* DIK_MEDIA_* */
  int32_t  health;              /* DiskStatus, DIK_HEALTH_* */
  int32_t  temperature_c;       /* DIK_NA if none */
  int32_t  life_percent;        /* DIK_NA if none */
  int32_t  rotation_rpm;        /* NominalMediaRotationRate if > 1; 1 means SSD -> DIK_NA; 0 -> DIK_NA */
  int64_t  power_on_hours;      /* DIK_NA64 if none */
  int64_t  power_on_count;      /* DIK_NA64 if not reported */
  int64_t  capacity_mb;         /* TotalDiskSize as is, decimal MB (the number the GUI prints); DIK_NA64 if 0 */
  int64_t  capacity_bytes;      /* NumberOfSectors * LogicalSectorSize; DIK_NA64 if not available (e.g. NVMe) */
  int64_t  host_reads_gb;       /* total host reads in GB of 2^30 bytes; DIK_NA64 if not reported */
  int64_t  host_writes_gb;      /* total host writes in GB of 2^30 bytes; DIK_NA64 if not reported */
  int64_t  nand_writes_gb;      /* total NAND writes in GB of 2^30 bytes; DIK_NA64 if not reported */
  uint32_t features;            /* DIK_FEAT_* */
  uint32_t access;              /* DIK_ACCESS_* */
  uint32_t source;              /* CommandType value */
  int32_t  attribute_count;     /* rows dik_attribute can return */
} dik_disk_t;

/* Caller sets struct_size to sizeof(dik_attribute_t) (must be >= v1 size).
   The DLL rejects smaller structs with DIK_E_STRUCT_SIZE and writes only its own size. */
typedef struct {
  uint32_t struct_size;
  uint8_t  id;
  uint8_t  raw_len;             /* 6, 7 or 8 bytes valid in raw[] (IsRawValues7/8; NVMe as Crystal stores it) */
  uint8_t  reserved[2];         /* explicit padding (0) */
  int32_t  current;             /* DIK_NA when not meaningful (NVMe) */
  int32_t  worst;               /* DIK_NA when not meaningful */
  int32_t  threshold;           /* DIK_NA when not available */
  uint8_t  raw[8];              /* little-endian bytes as Crystal stores them */
  uint8_t  reserved2[4];        /* explicit padding before 64-bit raw_value (0) */
  uint64_t raw_value;           /* raw[0..raw_len-1] as a little-endian integer */
  int32_t  status;              /* dicore::AttributeStatus: 0 unknown 1 good 2 caution 3 bad */
  uint8_t  reserved3[4];        /* explicit tail padding to 48 bytes (0) */
} dik_attribute_t;

uint32_t DIK_CALL dik_api_version(void);                  /* major << 16 | minor */
int  DIK_CALL dik_open(dik_handle** out, uint32_t flags);
void DIK_CALL dik_close(dik_handle* h);

/* Slow (seconds): enumerates every disk. Never call from a UI thread.
   Initializes COM on the calling thread on Windows. One thread per handle.
   Not isolated between handles: the scan calls CoInitializeSecurity (process-wide), may start
   the WMI service and uses file-level state, so serialize scans across the whole process.
   Returns DIK_E_ACCESS when not elevated and no disk was found (needs administrator). */
int  DIK_CALL dik_scan(dik_handle* h);

int  DIK_CALL dik_disk_count(const dik_handle* h);
int  DIK_CALL dik_disk(const dik_handle* h, int disk, dik_disk_t* out);
int  DIK_CALL dik_attribute(const dik_handle* h, int disk, int row, dik_attribute_t* out);

#ifdef __cplusplus
}
#endif

#endif /* DISKINFO_KIT_H */
