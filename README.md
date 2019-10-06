# Paradigm File Extractor

A program for extracting the files from any of Paradigm Entertainment's Nintendo 64 games. It should be able to extract the files from all of their games in all regions, i.e.
 * AeroFighters Assault
 * Beetle Adventure Racing!/HSV Adventure Racing!
 * Duck Dodgers Starring Daffy Duck/Daffy Duck Starring as Duck Dodgers
 * F-1 World Grand Prix
 * F-1 World Grand Prix II
 * Indy Racing 2000
 * Pilotwings 64
 * Sonic Wings Assault
 
To use, just drag an N64 ROM in z64 format onto the executable (or run it from the commandline with the input file as the sole parameter) and it will extract it to a folder with the same name as the ROM.

Some notes about the extracted output:
 * There will be two folders; Raw and Unpacked. Raw is the files in their native format (i.e. direct copies of their bytes from the ROM), and Unpacked does some additional processing to decompress compressed files and split files with multiple components into multiple files. I'm hoping at some point to also setup conversion of some of the files to more common formats; e.g. convert the images to PNGs, convert audio to WAV, etc. In the meantime, though, if you're looking to do that I know [PFedak and magcius have done some work on figuring out some of the formats in Pilotwings 64](https://github.com/magcius/noclip.website/blob/master/src/Pilotwings64/Scenes.ts), which might work for other games as well.
 * The games do not seem to have any folder structure or file naming system, just a table with references to file locations in the ROM. The way they're organized and named in the output folder is mostly just how I thought it made sense - I grouped them by their file type and named them using their location in the ROM. The names of the file types are either determined using debug info that was left in some of the games, or is just based on the header for the file's group in the file table. (There's one exception, though - the MODU files have something like a filename in them so I put that in the output file names for convenience).
 * The other stuff that gets put in the output folder (the stuff that's not in Raw or Unpacked) is just all of the parts of the ROM that *aren't* files. The data before the file table, the file table itself, and anything that comes after the last file (if there is any actual important data at all).
 
That's all! Feel free to open an issue or email me if you need help getting it working or have any questions. And if you use it to do something cool I'd love to know about it :)

Also, here are some potentially helpful notes for anyone who's looking to do some reverse engineering of the file types using this program:
 * The AeroFighters games (and maybe also Sonic Wings Assault, I haven't checked) have some really weird stuff going on with their files. There's files that are completely empty, files that have a bunch of empty data at the end for seemingly no reason, files with nonsensical headers, and maybe more stuff. Not sure why but it's something to be aware of.
 * If you look in the raw files, you may see some with GZIP sections - these are not actually GZIP encoded. They use MIO0 compression, starting at the MIO0 header.
 * The PAD section in the raw files seems to just be some sort of padding, maybe to align the bytes of the file? Regardless, they don't seem to ever contain anything other than 0s.
 * The COMM sections don't really have any consistent format, and I think COMM is probably just a generic section for any type of data (and I'd guess COMM is short for COMMON)
  * Some files just have some PAD sections and then one section with data - in some cases this section has a different header than the file itself, but regardless of whether it does, I wrote the program to use the file extension based on the main header. So if you're looking at a file in the Unpacked section that's been unpacked to one file, it might be worth checking its Raw counterpart for that extra header in case that gives you help figuring it out.
 * BAR, Duck Dodgers, Indy Racing 2000, and the F-1 Games all have MODU files in them which seem to be object files used in compilation, or at least code grouped into files with extra debugging info. Might be helpful!
 
 
 Also, the MIO0 compression is copied (and modified slightly) from [Mr Peeps' Compressor](https://github.com/Daniel-McCarthy/Mr-Peeps-Compressor).
