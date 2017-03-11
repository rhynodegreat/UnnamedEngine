using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnnamedEngine.Utilities {
    public class Node {
        public Node next;
        public ulong offset;
        public ulong size;
        public bool free;

        public Node(ulong offset, ulong size) {
            this.offset = offset;
            this.size = size;
            free = true;
        }

        public void Split(ulong start, ulong size) {
            //split a node and mark the correct one as not free
            //this node can potentially be split into three if start and size defines a space in the middle of the node

            if (start == offset && this.size == size) {
                //entire node was taken, so mark this as not free
                free = false;
            } else if (start > offset) {
                //some space was left in the beginning, so use this node for that and mark new one as not free
                ulong startSpace = start - offset;
                ulong middleSpace = this.size - startSpace;
                this.size = startSpace;

                Node middle = new Node(start, middleSpace);
                middle.next = next;
                next = middle;

                //new node might need to be split
                middle.Split(start, size);
            } else {
                //only some space left at the back
                free = false;
                ulong endOffset = start + size;
                ulong endSpace = (offset + this.size) - endOffset;
                this.size = size;

                Node end = new Node(endOffset, endSpace);
                end.next = next;
                next = end;
            }
        }

        public void Merge() {
            if (free) {
                Node next = this.next;
                while (next != null && next.free) {
                    size += next.size;
                    this.next = next.next;
                    next = next.next;
                }
            }
        }
    }
}
