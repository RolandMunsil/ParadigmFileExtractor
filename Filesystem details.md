# File System Details

The file system for Paradigm's N64 games is, overall, pretty simple. At a certain point in the ROM, 
there's a file table which lists the locations of all the files, and then immediately after that are 
all the files. !!TODO!! Some of the games have additional data after all of the files.

Note that there isn't any easily-findable consistent pointer to the file table in the Paradigm games - it's loaded in different
places in each game and in many or all cases is generated directly by the code (as opposed to being copied from somewhere in the ROM).
Thankfully, it's easy to find by looking for it's header.

## The File Table
It's important to not that there are actually two different file table formats - one used in TODO and the other used in TODO

### File Header Note
Before I explain the file table, just a note about how the files themselves are structured - except for some special cases which
I'll explain along the way, all entries in the file table are of the form

"FORM" length "TYPE" <file data> (TODO: make this clearer, maybe pic?)

Also, in TODO TODO TODO games, nearly all file types will start with "UV" - I assume "UV" is the term for their file system, or
engine, or something like that, since the "UV" part is also present in the names of the modules that actually load the files.

### Format 1
The file table itself actually follows the file table entry rules - it starts with "FORM" in ascii, followed by t
he length of the rest of the file table after these first 8 bytes, and after that is "UVFT" in ascii
(I assume the "FT" stands for File Table). 

After this, there's just a sequence of offset lists. These lists start with a four-letter ASCII string which is the 
type of all of the files in the list, and then a bunch of four-byte numbers which are offsets to the starts of files.
These offsets are from the first address ending in 0 after the end of the file table. So, for example, if the file 
table ended at `0x25FC8`, the offsets would be from `0x25FD0`.

TODO: images!

That's the basic format, but there are some important notes and exceptions. 

Firstly, the file offsets will sometimes just be `0xFFFFFFFF`.
They don'y appear to represent anything important, and as far as I can tell they can be safely ignored - you won't end up skipping any files
or data if you just ignore them. I have a theory about why they're there but I'll put it in a footnote since it's not really important to
parsing or reading the files. TODO: footnote

Secondly, some of the file types are unusual. The "UVRW" section appears to be a general section for any type of file, including some files
which don't have *any* header - not even "FORM" or a length. I assume "RW" is short for "Raw". So in short: files in the UVRW section
may have no header, and those that do have headers will not be "UVRW" files.

In addtion, two of the sections have a mismatch between the file type in the section and the file type of the actual files. The files
in the "MODU" section will actually have "UVMO" in their header, and some of the files in the "UVSQ" section will actually have "UVTS" in
their header.

### Format 2
In the rest of the games, the file table is an almost compltely different format. Firstly, the file table is actually compressed using
MIO0 compression - it's compressed like any other MIO0 compressed file in the Paradigm games so you can refer to the section below
about compressed files for information about decompressing it. It has the "UVRM" file type (as opposed to the "UVFT" file type of the
other file table format)

Once you decompress it, you get what is essentially a big array of type-size pairings.

TODO: image!

The four-letter string is the file type of the file, and the 4-byte number is the size of the file table entry (IMPORTANT: 
this means it *does* include the length of the "FORM" string at the start and the 4-byte length after it). Like the other file table 
format, the first file starts at the first address ending in 0 after the end of the file table. These pairs are in the same order as 
the files are in the ROM.

#### AeroWings empty space after files TODO TODO

## The Files
I don't understand much about any of the specific file formats at this point, (TODO reference jasper's code), but all of the files
use a common structure.

After the 4-byte string indicating the file type, the file consists of a series of sections which follow the format (four byte section
type string) (section length) (data) TODO: images, make more clear. Again, I don't know what most of these section types mean, but
there are a few that I do (and which are important to understand)

### "GZIP" sections aka MIO0 sections
Sections that start with "GZIP" are, confusingly, actually files compressed using the MIO0 compression format. After the "GZIP" and the length,
the next bit is the filetype of the compressed file and the original length of the file (before compression) (TODO: is this correct?), and 
then the actual MIO0 data. I assume that at some point they were using or were planning on using the "GZIP" format at some point, 
and when they switched to MIO0 they kept the old header.

### "PAD " sections

"PAD " sections, in every case I've seen, have just 4 bytes of data which is always `0x00000000` (TODO: check). Given the name "PAD", it
seems like they're probably there as padding, maybe for alignment?

### "COMM" sections
It seems like "COMM" is a catch-all for any type of data without a defined type - "COMM" is probably short for "COMMON".

THEORY:
 * multi-region games
 * files needed in some versions but not others
 * CHECK!
  
  
  
  
