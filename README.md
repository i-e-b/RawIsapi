# RawIsapi
Example of a raw ISAPI module communicating with a .Net assembly

## To use:

* Set the (currently hard-coded) path in `RawIsapi.cpp` to point at the managed dll
* Build the project.
* Make a new IIS site pointing at the folder that holds the `web.config` file
* Set the app pool for the site to `Classic` and `No Managed Code`
* Set permissions on the built dlls so the app pool user can access them
* Set `ISAPI and CGI Restrictions` on the IIS server to allow the dll to run.

If you get `cannot open...` errors when rebuilding, recycle the IIS app pool.

## Permissions

Make sure the native dll, .Net dll and web.config files all can be read by the IIS app pool process.
Check user permissions for the Huygens hosted site, and under `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Temporary ASP.NET Files`.
Make sure the temp ASP.NET folder has modify permissions. The hosted site only needs read and execute

## To do:

* [x] Handle incoming data larger than initial buffer
* [x] Link the ecb to .Net startup if first try failed
* [x] Remove/reduce hard-coded paths
* [x] Prove that we can call Huygens-hosted ASP.Net apps from the shim
* [x] Find a way to get errors thrown by the CLR side