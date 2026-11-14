#!/bin/bash
cd "$(dirname "$0")"
for sp in apple pear cherry; do for v in 1 2; do for st in leaf bloom ripe; do
  suf=$([ $v = 2 ] && echo _2); name=Tree_${sp}_tree_${st}${suf}
  ~/bpyenv/bin/python tree3d.py $sp $st $v $PWD/out/${sp}_${st}_$v.png > logs_$name.txt 2>&1; [ -f out/${sp}_${st}_$v.png ] || { echo FAIL $name; continue; }
  ~/sdenv/bin/python place.py out/${sp}_${st}_$v.png $name tree 1.0 placed/$name.png > /dev/null && echo done $name
done; done; done
