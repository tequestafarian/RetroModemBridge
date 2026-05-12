# Monthly Untouched + Daily Sidecar

This build starts from the last build where Monthly worked.

Monthly update path:

- unchanged from the working Monthly build
- uses the original guide link discovery
- uses the original DownloadAndInstallFromUrlAsync path
- uses the original InstallZip path
- expects and extracts bbslist.csv

Daily update path:

- completely separate sidecar path
- searches specifically for visible anchor text `Download Daily`
- downloads that URL
- expects dialdirectory.xml
- parses Daily XML and normalizes it to current-bbslist.csv

The goal is to fix Daily without touching Monthly at all.
